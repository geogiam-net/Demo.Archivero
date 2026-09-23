using Demo.Archivero.Application.Dtos;

namespace Demo.Archivero.Application.Interfaces.Application;

public interface IWordFileService
{
    Task<ResultDto<bool>> CreateWordFileAsync(int fileId, string username, CancellationToken ct);

    Task<ResultDto<bool>> DeleteWordFileAsync(int fileId, string username, CancellationToken ct);
}