namespace Demo.Archivero.Infrastructure.ServiceBus;

public sealed class FileBusSettings
{
    public const string FileBusConfiguration = "ArchiveroFileBus";

    public required string ConnectionString { get; set; }
    public required string CreateWordFileQueueName { get; set; }
    public required string DeleteWordFileQueueName { get; set; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(CreateWordFileQueueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeleteWordFileQueueName);
        if (string.Equals(CreateWordFileQueueName, DeleteWordFileQueueName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Create and delete operations require different queues.");
    }
}
