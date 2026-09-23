using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Domain.Entities;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Interfaces.Repositories;

public interface IFileRepository
{
    Task<FileEntity?> GetFileAsync(int id, CancellationToken ct);

    Task<List<FileEntity>> GetFilesAsync(int owner, CancellationToken ct);

    Task<ResultDto<int>> SetInQueueFileAsync(FileEntity file, AppUser user, CancellationToken ct);

    Task<ResultDto<bool>> ReadyFileAsync(FileEntity file, AppUser user, CancellationToken ct);

    Task<ResultDto<bool>> SetFileAsObsoleteAsync(int id, AppUser user, CancellationToken ct);

    Task<ResultDto<bool>> DeleteFileAsync(int id, CancellationToken ct);
}
