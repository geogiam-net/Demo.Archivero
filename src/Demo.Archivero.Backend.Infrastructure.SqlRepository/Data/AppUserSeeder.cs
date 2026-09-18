using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Domain.Entities;

namespace Demo.Archivero.Infrastructure.SqlRepository.Data;

public static class AppUserSeeder
{
    public static async Task SeedAsync(
        DbContextArchivero db,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var username = configuration["ArchiveroAuth:AdminUsername"];
        var password = configuration["ArchiveroAuth:AdminPassword"];
        
        if(username is null || password is null)
            return;

        await EnsureUserAsync(db, username, password, AppRoles.Admin, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUserAsync(
        DbContextArchivero db,
        string username,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        var existing = await db.AppUsers
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (existing is null)
        {
            db.AppUsers.Add(new AppUser
            {
                Username = username,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role,
                IsActive = true
            });
            return;
        }

        existing.Role = role;
        existing.IsActive = true;
    }

    private sealed record SeedUser(
        string Username,
        string Password,
        string Role,
        string? MaintenanceSkills = null);
}
