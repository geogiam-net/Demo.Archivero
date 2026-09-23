using Azure.Messaging.ServiceBus;
using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Converter.Worker.Services;
using Demo.Archivero.Converter.Worker.Startup;
using Demo.Archivero.Converter.Worker.Workers;
using Demo.Archivero.Domain.Enums;
using Demo.Archivero.Infrastructure.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Demo.Archivero.UnitTests;

public class ConverterWorkerTests
{
    [TestCase(false, true, Error.None, true)]
    [TestCase(true, true, Error.None, true)]
    [TestCase(false, false, Error.None, false)]
    [TestCase(true, false, Error.InternalServerError, false)]
    [TestCase(false, true, Error.Conflict, false)]
    [TestCase(true, true, Error.NotFound, false)]
    public async Task Handler_dispatches_endpoint_logic_and_only_acknowledges_success(
        bool delete, bool result, Error error, bool expected)
    {
        var service = new WordService { Result = new(result, error) };
        var handler = new WordFileMessageHandler(service, NullLogger<WordFileMessageHandler>.Instance);
        using var cancellation = new CancellationTokenSource();
        var message = new WordFileMessage(42, "alice");
        var succeeded = delete
            ? await handler.DeleteWordFileAsync(message, "operation", cancellation.Token)
            : await handler.CreateWordFileAsync(message, "operation", cancellation.Token);
        Assert.That(succeeded, Is.EqualTo(expected));
        Assert.That(service.Call, Is.EqualTo((delete, 42, "alice", cancellation.Token)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Handler_does_not_swallow_processing_exceptions(bool delete)
    {
        var service = new WordService { Error = new InvalidOperationException("Database unavailable") };
        var handler = new WordFileMessageHandler(service, NullLogger<WordFileMessageHandler>.Instance);
        Assert.ThrowsAsync<InvalidOperationException>(() => delete
            ? handler.DeleteWordFileAsync(new(42, "alice"), "operation", default)
            : handler.CreateWordFileAsync(new(42, "alice"), "operation", default));
    }

    [Test]
    public async Task Two_hosted_workers_independently_start_and_stop_their_queues()
    {
        var client = new BusClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ServiceBusClient>(client);
        services.AddSingleton(new FileBusSettings
        {
            ConnectionString = "unused-by-test-client",
            CreateWordFileQueueName = "create",
            DeleteWordFileQueueName = "delete"
        });
        services.AddFileWorkers();
        await using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IHostedService>().ToArray();
        Assert.That(workers.Select(w => w.GetType()), Is.EquivalentTo(new[]
        {
            typeof(CreateWordFileWorker), typeof(DeleteWordFileWorker)
        }));
        var create = workers.OfType<CreateWordFileWorker>().Single();
        var delete = workers.OfType<DeleteWordFileWorker>().Single();
        await create.StartAsync(default);
        Assert.That(client.Processors["create"].Running, Is.True);
        Assert.That(client.Processors["delete"].Running, Is.False);
        await delete.StartAsync(default);
        await create.StopAsync(default);
        Assert.That(client.Processors["create"].Running, Is.False);
        Assert.That(client.Processors["delete"].Running, Is.True);
        await delete.StopAsync(default);
        Assert.That(client.Processors["delete"].Running, Is.False);
    }

    private sealed class WordService : IWordFileService
    {
        public ResultDto<bool> Result { get; init; } = new(true);
        public Exception? Error { get; init; }
        public (bool Delete, int Id, string Username, CancellationToken Ct) Call { get; private set; }
        public Task<ResultDto<bool>> CreateWordFileAsync(int fileId, string username, CancellationToken ct)
            => Handle(false, fileId, username, ct);
        public Task<ResultDto<bool>> DeleteWordFileAsync(int fileId, string username, CancellationToken ct)
            => Handle(true, fileId, username, ct);
        private Task<ResultDto<bool>> Handle(bool delete, int id, string username, CancellationToken ct)
        {
            Call = (delete, id, username, ct);
            return Error is null ? Task.FromResult(Result) : Task.FromException<ResultDto<bool>>(Error);
        }
    }

    private sealed class BusClient : ServiceBusClient
    {
        public Dictionary<string, Processor> Processors { get; } = new();
        public override ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
            => Processors[queueName] = new Processor();
    }

    private sealed class Processor : ServiceBusProcessor
    {
        public bool Running { get; private set; }
        public override Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            Running = true;
            return Task.CompletedTask;
        }
        public override Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            Running = false;
            return Task.CompletedTask;
        }
        public override Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
