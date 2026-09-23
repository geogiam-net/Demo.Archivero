using Azure.Messaging.ServiceBus;
using Demo.Archivero.Application.Interfaces.Infrastructure;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed class CreateWordFileSender(ServiceBusClient client, FileBusSettings settings)
    : FileBusSenderBase(client, settings.CreateWordFileQueueName, MessageSubject), ICreateWordFileSender
{
    internal const string MessageSubject = "CreateWordFile";

    public Task SendCreateWordFileAsync(int fileId, string username, string messageId, CancellationToken ct)
        => SendAsync(new WordFileMessage(fileId, username), messageId, ct);
}
