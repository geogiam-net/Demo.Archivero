namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IDeleteWordFileSender
{
    Task SendDeleteWordFileAsync(int fileId, string username, string messageId, CancellationToken ct);
}
