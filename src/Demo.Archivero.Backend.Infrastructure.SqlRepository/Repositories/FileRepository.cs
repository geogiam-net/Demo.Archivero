using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Microsoft.EntityFrameworkCore;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Infrastructure.SqlRepository.Repositories;

public sealed class FileRepository(DbContextArchivero db) : IFileRepository
{
    public async Task CreateFileAsync(FileEntity file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);

        db.Files.Add(file);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFileAsync(int id, CancellationToken ct)
    {
        var file = await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (file is null)
        {
            throw new InvalidOperationException("File not found.");
        }

        db.Files.Remove(file);

        await db.SaveChangesAsync(ct);
    }
}
