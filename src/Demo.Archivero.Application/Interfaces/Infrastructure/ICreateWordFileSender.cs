namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface ICreateWordFileSender
{
    Task SendCreateWordFileAsync(int fileId, string username, string messageId, CancellationToken ct);
}
