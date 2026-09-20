using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Domain.Enums;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Microsoft.EntityFrameworkCore;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Infrastructure.SqlRepository.Repositories;

public sealed class FileRepository(DbContextArchivero db) : IFileRepository
{
    public async Task<List<FileEntity>> GetFilesAsync(int owner, CancellationToken ct)
    {
        return await db.Files.AsNoTracking().Where(x => x.OwnerId == owner && x.Status == FileStatus.Available).ToListAsync(ct);
    }

    public async Task CreateFileAsync(FileEntity file, CancellationToken ct)
    {
        file.Status = FileStatus.Available;
        db.Files.Add(file);

        await db.SaveChangesAsync(ct);
    }

    public async Task<ResultDto<bool>> SetFileAsObsoleteAsync(int id, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (file is null)
        {
            return new ResultDto<bool>(
                false,
                Error.NotFound,
                new List<string> { "File not found." }
            );
        }

        file.Status = FileStatus.Obsolete;

        await db.SaveChangesAsync(ct);

        return new ResultDto<bool>(true);
    }

    public async Task<ResultDto<bool>> DeleteFileAsync(int id, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (file is null)
        {
            return new ResultDto<bool>(
                false,
                Error.NotFound,
                new List<string> { "File not found." }
            );
        }

        db.Files.Remove(file);

        await db.SaveChangesAsync(ct);

        return new ResultDto<bool>(true);
    }
}
