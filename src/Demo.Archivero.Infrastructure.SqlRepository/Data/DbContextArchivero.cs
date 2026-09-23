using Microsoft.EntityFrameworkCore;
using Demo.Archivero.Domain.Entities;
using Demo.Archivero.Domain.Entities.Bases;
using Demo.Archivero.Domain.Enums;
using Demo.Archivero.Application.Settings;
using File = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Infrastructure.SqlRepository.Data;

public sealed class DbContextArchivero(DbContextOptions<DbContextArchivero> options) : DbContext(options)
{
    public const string DbSchema = "dbo";

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<File> Files => Set<File>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(DbSchema);

        b.UseCollation("Latin1_General_100_CI_AS");

        ConfigureAppUser(b);
        ConfigureFile(b);

        base.OnModelCreating(b);
    }

    private static void ConfigureAppUser(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers");

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();

            e.Property(x => x.Username)
                .HasMaxLength(256)
                .IsRequired();

            e.HasIndex(x => x.Username).IsUnique();

            e.Property(x => x.PasswordHash)
                .HasMaxLength(512)
                .IsRequired();

            e.Property(x => x.Role)
                .HasMaxLength(64)
                .HasDefaultValue("Viewer")
                .IsRequired();

            e.Property(x => x.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            e.Property(x => x.LastLoginAtUtc).HasColumnType("datetime2");

            ConfigureAudit(e);
        });
    }

    private static void ConfigureFile(ModelBuilder b)
    {
        b.Entity<File>(e =>
        {
            e.ToTable("Files");

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();

            e.Property(x => x.Title)
                .HasMaxLength(EntitiesSettings.FileTitleMaxLength)
                .IsRequired();

            e.Property(x => x.Content)
                .HasMaxLength(EntitiesSettings.FileContentMaxLength)
                .IsRequired();

            e.Property(x => x.Status)
                .HasConversion<short>()
                .HasDefaultValue(FileStatus.None)
                .IsRequired();

            e.Property(x => x.BlobId)
                .HasMaxLength(256)
                .IsRequired();

            e.HasIndex(x => x.OwnerId);

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureAudit(e);
        });
    }

    // Shared audit columns declared on PersistenceBase.
    private static void ConfigureAudit<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e)
        where T : EntityBase
    {
        e.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();
        e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        e.Property(x => x.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();
        e.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
