namespace Demo.Archivero.Infrastructure.ServiceBus;

/// <summary>
/// Implementations must be idempotent: the same message ID can be delivered more than once.
/// Return true only after all work is durably committed (or was already committed).
/// Return false or throw when processing fails; the message will not be completed.
/// </summary>
public interface IWordFileMessageHandler
{
    Task<bool> CreateWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct);
    Task<bool> DeleteWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct);
}
