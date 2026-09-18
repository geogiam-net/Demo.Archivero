using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Interfaces.Repositories;

public interface IFileRepository
{
    Task CreateFileAsync(FileEntity file, CancellationToken ct);

    Task DeleteFileAsync(int id, CancellationToken ct);
}
