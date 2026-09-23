namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed record WordFileMessage(int FileId, string Username)
{
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(FileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Username);
    }
}
