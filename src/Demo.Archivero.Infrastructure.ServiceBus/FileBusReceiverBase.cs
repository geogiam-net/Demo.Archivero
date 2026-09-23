using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public abstract class FileBusReceiverBase : IHostedService, IAsyncDisposable
{
    private readonly ServiceBusProcessor processor;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly string expectedSubject;

    protected FileBusReceiverBase(ServiceBusClient client, string queueName, string subject,
        IServiceScopeFactory scopeFactory, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        expectedSubject = subject;
        processor = client.CreateProcessor(queueName, CreateProcessorOptions());
        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;
    }
    internal static ServiceBusProcessorOptions CreateProcessorOptions() => new()
    {
        ReceiveMode = ServiceBusReceiveMode.PeekLock,
        AutoCompleteMessages = false,
        PrefetchCount = 0,
        MaxConcurrentCalls = 1,
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(30)
    };

    public Task StartAsync(CancellationToken ct) => processor.StartProcessingAsync(ct);
    public Task StopAsync(CancellationToken ct) => processor.StopProcessingAsync(ct);
    internal async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        args.CancellationToken.ThrowIfCancellationRequested();
        WordFileMessage message;
        try
        {
            if (args.Message.Subject != expectedSubject || string.IsNullOrWhiteSpace(args.Message.MessageId))
                throw new JsonException("Missing message ID or incorrect operation for this queue.");
            message = args.Message.Body.ToObjectFromJson<WordFileMessage>()
                ?? throw new JsonException("Message body is null.");
            message.Validate();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            logger.LogWarning("Dead-lettering invalid {Operation} message {MessageId}.",
                expectedSubject, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidWordFileMessage",
                "Expected the correct operation subject and a JSON body with positive FileId and nonempty Username.",
                args.CancellationToken);
            return;
        }

        // A fresh scope per delivery permits scoped repositories/DbContexts in the handler.
        // Exceptions deliberately escape: the SDK abandons failed handler deliveries,
        // even with automatic completion disabled. If settlement fails, the lock expires.
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IWordFileMessageHandler>();
        var succeeded = await HandleAsync(handler, message, args.Message.MessageId, args.CancellationToken);

        args.CancellationToken.ThrowIfCancellationRequested();
        if (succeeded)
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        else
        {
            logger.LogWarning("Retrying {Operation} message {MessageId}, delivery {DeliveryCount}.",
                expectedSubject, args.Message.MessageId, args.Message.DeliveryCount);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error on {EntityPath} during {ErrorSource}.",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    protected abstract Task<bool> HandleAsync(IWordFileMessageHandler handler, WordFileMessage message,
        string messageId, CancellationToken ct);

    public ValueTask DisposeAsync() => processor.DisposeAsync();
}
