using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed class DeleteWordFileReceiver(ServiceBusClient client, FileBusSettings settings,
    IServiceScopeFactory scopeFactory, ILogger<DeleteWordFileReceiver> logger)
    : FileBusReceiverBase(client, settings.DeleteWordFileQueueName,
        DeleteWordFileSender.MessageSubject, scopeFactory, logger)
{
    protected override Task<bool> HandleAsync(IWordFileMessageHandler handler, WordFileMessage message,
        string messageId, CancellationToken ct)
        => handler.DeleteWordFileAsync(message, messageId, ct);
}
