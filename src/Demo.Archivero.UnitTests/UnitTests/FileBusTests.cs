using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Demo.Archivero.Infrastructure.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Demo.Archivero.UnitTests;

public class FileBusTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Resolving_a_sender_initializes_only_its_own_queue(bool delete)
    {
        var client = new BusClient();
        var services = new ServiceCollection();
        services.AddSingleton<ServiceBusClient>(client);
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArchiveroFileBus:ConnectionString"] = Settings.ConnectionString,
                ["ArchiveroFileBus:CreateWordFileQueueName"] = Settings.CreateWordFileQueueName,
                ["ArchiveroFileBus:DeleteWordFileQueueName"] = Settings.DeleteWordFileQueueName
            }).Build();
        services.AddArchiveroFileBus(config);
        await using var provider = services.BuildServiceProvider();
        if (delete)
            provider.GetRequiredService<Demo.Archivero.Application.Interfaces.Infrastructure.IDeleteWordFileSender>();
        else
            provider.GetRequiredService<Demo.Archivero.Application.Interfaces.Infrastructure.ICreateWordFileSender>();
        Assert.That(client.Senders.Keys, Is.EqualTo(new[]
        {
            delete ? Settings.DeleteWordFileQueueName : Settings.CreateWordFileQueueName
        }));
        Assert.That(client.Options, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Receiver_initializes_only_its_own_queue(bool delete)
    {
        var client = new BusClient();
        await using var provider = BuildProvider(new Handler());
        await using var receiver = CreateReceiver(provider, client,
            delete ? DeleteWordFileSender.MessageSubject : CreateWordFileSender.MessageSubject);
        Assert.That(client.Options.Keys, Is.EqualTo(new[]
        {
            delete ? Settings.DeleteWordFileQueueName : Settings.CreateWordFileQueueName
        }));
        Assert.That(client.Senders, Is.Empty);
    }

    private static FileBusSettings Settings => new()
    {
        ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==",
        CreateWordFileQueueName = "create-word-file",
        DeleteWordFileQueueName = "delete-word-file"
    };

    [TestCase(CreateWordFileSender.MessageSubject)]
    [TestCase(DeleteWordFileSender.MessageSubject)]
    public async Task Successful_handler_completes_the_correct_operation(string operation)
    {
        var handler = new Handler();
        await using var provider = BuildProvider(handler);
        await using var receiver = CreateReceiver(provider, operation: operation);
        var args = new Delivery(operation);
        await receiver.ProcessMessageAsync(args);
        Assert.That(args.Completed, Is.EqualTo(1));
        Assert.That(args.Abandoned, Is.Zero);
        Assert.That(handler.Operation, Is.EqualTo(operation));
        Assert.That(handler.MessageId, Is.EqualTo("operation-123"));
        Assert.That(handler.Message, Is.EqualTo(new WordFileMessage(42, "alice")));
    }

    [Test]
    public async Task Unsuccessful_handler_abandons_without_completing()
    {
        await using var provider = BuildProvider(new Handler { Result = false });
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery();
        await receiver.ProcessMessageAsync(args);
        Assert.That(args.Completed, Is.Zero);
        Assert.That(args.Abandoned, Is.EqualTo(1));
    }

    [Test]
    public async Task Handler_exception_escapes_for_sdk_retry_without_completing()
    {
        await using var provider = BuildProvider(new Handler { Error = new InvalidOperationException("Database unavailable") });
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery();
        Assert.ThrowsAsync<InvalidOperationException>(() => receiver.ProcessMessageAsync(args));
        Assert.That(args.Completed, Is.Zero);
        Assert.That(args.DeadLettered, Is.Zero);
    }

    [TestCase("null")]
    [TestCase("not json")]
    [TestCase("{\"FileId\":0,\"Username\":\"alice\"}")]
    [TestCase("{\"FileId\":42}")]
    public async Task Invalid_payload_is_preserved_in_dead_letter_queue(string body)
    {
        var handler = new Handler();
        await using var provider = BuildProvider(handler);
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery(body: body);
        await receiver.ProcessMessageAsync(args);
        Assert.That(args.DeadLettered, Is.EqualTo(1));
        Assert.That(args.Completed, Is.Zero);
        Assert.That(handler.Operation, Is.Null);
    }

    [Test]
    public async Task Wrong_queue_operation_is_dead_lettered()
    {
        await using var provider = BuildProvider(new Handler());
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery(DeleteWordFileSender.MessageSubject);
        await receiver.ProcessMessageAsync(args);
        Assert.That(args.DeadLettered, Is.EqualTo(1));
        Assert.That(args.Completed, Is.Zero);
    }

    [Test]
    public async Task Cancellation_during_processing_does_not_complete()
    {
        using var cancellation = new CancellationTokenSource();
        await using var provider = BuildProvider(new Handler { BeforeReturn = cancellation.Cancel });
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery(ct: cancellation.Token);
        Assert.ThrowsAsync<OperationCanceledException>(() => receiver.ProcessMessageAsync(args));
        Assert.That(args.Completed, Is.Zero);
    }

    [Test]
    public async Task Lock_loss_during_completion_is_not_hidden_or_dead_lettered()
    {
        await using var provider = BuildProvider(new Handler());
        await using var receiver = CreateReceiver(provider);
        var args = new Delivery { CompletionError = new ServiceBusException("Lock lost", ServiceBusFailureReason.MessageLockLost) };
        Assert.ThrowsAsync<ServiceBusException>(() => receiver.ProcessMessageAsync(args));
        Assert.That(args.Completed, Is.Zero);
        Assert.That(args.DeadLettered, Is.Zero);
    }

    [Test]
    public async Task Receiver_uses_manual_peek_lock_without_prefetch_for_both_queues()
    {
        var client = new BusClient();
        await using var provider = BuildProvider(new Handler());
        await using var receiver = CreateReceiver(provider, client);
        await using var deleteReceiver = CreateReceiver(provider, client, DeleteWordFileSender.MessageSubject);
        Assert.That(client.Options.Keys, Is.EquivalentTo(new[] { Settings.CreateWordFileQueueName, Settings.DeleteWordFileQueueName }));
        foreach (var options in client.Options.Values)
        {
            Assert.That(options.ReceiveMode, Is.EqualTo(ServiceBusReceiveMode.PeekLock));
            Assert.That(options.AutoCompleteMessages, Is.False);
            Assert.That(options.PrefetchCount, Is.Zero);
            Assert.That(options.MaxAutoLockRenewalDuration, Is.EqualTo(TimeSpan.FromMinutes(30)));
        }
    }

    [Test]
    public async Task Sender_routes_operations_and_preserves_the_retry_id_and_body()
    {
        var client = new BusClient();
        await using var sender = new CreateWordFileSender(client, Settings);
        var message = new WordFileMessage(42, "alice");
        await sender.SendAsync(message, "create-123");
        await sender.SendAsync(message, "create-123");
        await using var deleteSender = new DeleteWordFileSender(client, Settings);
        await deleteSender.SendAsync(message, "delete-456");
        var creates = client.Senders[Settings.CreateWordFileQueueName].Messages;
        var deletes = client.Senders[Settings.DeleteWordFileQueueName].Messages;
        Assert.That(creates.Select(m => m.MessageId), Is.EqualTo(new[] { "create-123", "create-123" }));
        Assert.That(creates[0].Body.ToObjectFromJson<WordFileMessage>(), Is.EqualTo(message));
        Assert.That(creates[0].Subject, Is.EqualTo(CreateWordFileSender.MessageSubject));
        Assert.That(deletes.Single().Subject, Is.EqualTo(DeleteWordFileSender.MessageSubject));
        Assert.That(deletes.Single().MessageId, Is.EqualTo("delete-456"));
    }

    [Test]
    public async Task Send_failure_reaches_the_caller()
    {
        var client = new BusClient();
        await using var sender = new CreateWordFileSender(client, Settings);
        client.Senders[Settings.CreateWordFileQueueName].Error = new ServiceBusException("Unavailable", ServiceBusFailureReason.ServiceCommunicationProblem);
        Assert.ThrowsAsync<ServiceBusException>(() => sender.SendAsync(new(42, "alice"), "create-123"));
    }

    [Test]
    public void Same_queue_for_both_operations_is_rejected()
    {
        var settings = Settings;
        settings.DeleteWordFileQueueName = settings.CreateWordFileQueueName;
        Assert.Throws<ArgumentException>(() => settings.Validate());
    }

    private static ServiceProvider BuildProvider(Handler handler)
        => new ServiceCollection().AddScoped<IWordFileMessageHandler>(_ => handler).BuildServiceProvider();

    private static FileBusReceiverBase CreateReceiver(ServiceProvider provider, BusClient? client = null,
        string operation = CreateWordFileSender.MessageSubject)
        => operation == CreateWordFileSender.MessageSubject
            ? new CreateWordFileReceiver(client ?? new BusClient(), Settings, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CreateWordFileReceiver>.Instance)
            : new DeleteWordFileReceiver(client ?? new BusClient(), Settings, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DeleteWordFileReceiver>.Instance);

    private sealed class Handler : IWordFileMessageHandler
    {
        public bool Result { get; init; } = true;
        public Exception? Error { get; init; }
        public Action? BeforeReturn { get; init; }
        public string? Operation { get; private set; }
        public string? MessageId { get; private set; }
        public WordFileMessage? Message { get; private set; }
        public Task<bool> CreateWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct)
            => Handle(message, messageId, CreateWordFileSender.MessageSubject);
        public Task<bool> DeleteWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct)
            => Handle(message, messageId, DeleteWordFileSender.MessageSubject);
        private Task<bool> Handle(WordFileMessage message, string messageId, string operation)
        {
            Operation = operation;
            MessageId = messageId;
            Message = message;
            BeforeReturn?.Invoke();
            return Error is null ? Task.FromResult(Result) : Task.FromException<bool>(Error);
        }
    }

    private sealed class BusClient : ServiceBusClient
    {
        public Dictionary<string, Sender> Senders { get; } = new();
        public Dictionary<string, ServiceBusProcessorOptions> Options { get; } = new();
        public override ServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions? options = null)
            => Senders[queueOrTopicName] = new Sender();
        public override ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
        {
            Options[queueName] = options;
            return new Processor();
        }
    }

    private sealed class Processor : ServiceBusProcessor
    {
        public override Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Sender : ServiceBusSender
    {
        public List<ServiceBusMessage> Messages { get; } = new();
        public Exception? Error { get; set; }
        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            if (Error is not null) return Task.FromException(Error);
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Delivery : ProcessMessageEventArgs
    {
        public int Completed { get; private set; }
        public int Abandoned { get; private set; }
        public int DeadLettered { get; private set; }
        public Exception? CompletionError { get; init; }
        public Delivery(string subject = CreateWordFileSender.MessageSubject,
            string body = "{\"FileId\":42,\"Username\":\"alice\"}", CancellationToken ct = default)
            : base(ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body),
                messageId: "operation-123", subject: subject), null!, "test", ct) { }
        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            if (CompletionError is not null) return Task.FromException(CompletionError);
            Completed++;
            return Task.CompletedTask;
        }
        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message,
            IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            Abandoned++;
            return Task.CompletedTask;
        }
        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason,
            string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
        {
            DeadLettered++;
            return Task.CompletedTask;
        }
    }
}

