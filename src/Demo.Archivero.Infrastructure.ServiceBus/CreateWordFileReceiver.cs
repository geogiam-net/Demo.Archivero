using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed class CreateWordFileReceiver(ServiceBusClient client, FileBusSettings settings,
    IServiceScopeFactory scopeFactory, ILogger<CreateWordFileReceiver> logger)
    : FileBusReceiverBase(client, settings.CreateWordFileQueueName,
        CreateWordFileSender.MessageSubject, scopeFactory, logger)
{
    protected override Task<bool> HandleAsync(IWordFileMessageHandler handler, WordFileMessage message,
        string messageId, CancellationToken ct)
        => handler.CreateWordFileAsync(message, messageId, ct);
}
