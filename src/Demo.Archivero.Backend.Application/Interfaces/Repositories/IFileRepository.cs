using Demo.Archivero.Application.Dtos;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Interfaces.Repositories;

public interface IFileRepository
{
    Task<List<FileEntity>> GetFilesAsync(int owner, CancellationToken ct);

    Task CreateFileAsync(FileEntity file, CancellationToken ct);

    Task<ResultDto<bool>> SetFileAsObsoleteAsync(int id, CancellationToken ct);

    Task<ResultDto<bool>> DeleteFileAsync(int id, CancellationToken ct);
}
