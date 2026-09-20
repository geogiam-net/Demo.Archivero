using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.File;

namespace Demo.Archivero.Application.Interfaces.Application;

public interface IFileService
{
    Task<ResultDto<bool>> CreateFileAsync(string title, string content, string username, CancellationToken ct);

    Task<ResultDto<IReadOnlyList<FileDto>>> GetFilesAsync(string username, CancellationToken ct);

    Task<ResultDto<bool>> DeleteFileAsync(int fileId, string username, CancellationToken ct);
}