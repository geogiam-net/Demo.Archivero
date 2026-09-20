using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Domain.Entities;
using Demo.Archivero.Domain.Enums;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Microsoft.EntityFrameworkCore;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Infrastructure.SqlRepository.Repositories;

public sealed class FileRepository(DbContextArchivero db, IDateTimeProvider dateTimeProvider) : IFileRepository
{
    public async Task<FileEntity?> GetFileAsync(int id, CancellationToken ct)
    {
        return await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<FileEntity>> GetFilesAsync(int owner, CancellationToken ct)
    {
        return await db.Files.AsNoTracking().Where(x => x.OwnerId == owner && x.Status == FileStatus.Available).ToListAsync(ct);
    }

    public async Task<ResultDto<int>> CreateFileAsync(FileEntity file, AppUser user, CancellationToken ct)
    {
        file.Status = FileStatus.Available;
        file.OwnerId = user.Id;
        file.CreatedAtUtc = dateTimeProvider.UtcNow;
        file.CreatedBy = user.Username;

        db.Files.Add(file);

        await db.SaveChangesAsync(ct);

        return new ResultDto<int>(file.Id);
    }

    public async Task<ResultDto<bool>> SetFileAsObsoleteAsync(int id, AppUser user, CancellationToken ct)
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
        file.UpdatedAtUtc = dateTimeProvider.UtcNow;
        file.UpdatedBy = user.Username;

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
