using Azure.Messaging.ServiceBus;
using Demo.Archivero.Application.Interfaces.Infrastructure;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed class DeleteWordFileSender(ServiceBusClient client, FileBusSettings settings)
    : FileBusSenderBase(client, settings.DeleteWordFileQueueName, MessageSubject), IDeleteWordFileSender
{
    internal const string MessageSubject = "DeleteWordFile";

    public Task SendDeleteWordFileAsync(int fileId, string username, string messageId, CancellationToken ct)
        => SendAsync(new WordFileMessage(fileId, username), messageId, ct);
}
