using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demo.Archivero.Infrastructure.SqlRepository.Data;

public sealed class DbContextFactory : IDesignTimeDbContextFactory<DbContextArchivero>
{
    public DbContextArchivero CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DbContextArchivero>()
            .UseSqlServer("")
            .Options;

        return new DbContextArchivero(options);
    }
}
