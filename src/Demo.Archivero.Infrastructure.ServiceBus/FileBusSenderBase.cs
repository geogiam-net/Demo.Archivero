using Azure.Messaging.ServiceBus;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public abstract class FileBusSenderBase : IAsyncDisposable
{
    private readonly ServiceBusSender sender;
    private readonly string subject;

    protected FileBusSenderBase(ServiceBusClient client, string queueName, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        sender = client.CreateSender(queueName);
        this.subject = subject;
    }

    public Task SendAsync(WordFileMessage message, string messageId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (messageId.Length > 128)
            throw new ArgumentException("Message IDs cannot exceed 128 characters.", nameof(messageId));
        return sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
        {
            MessageId = messageId,
            Subject = subject,
            ContentType = "application/json",
            TimeToLive = TimeSpan.MaxValue
        }, ct);
    }

    public ValueTask DisposeAsync() => sender.DisposeAsync();
}
