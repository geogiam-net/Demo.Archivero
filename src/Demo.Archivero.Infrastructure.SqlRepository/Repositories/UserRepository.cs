using Microsoft.EntityFrameworkCore;
using Demo.Archivero.Application.Dtos.User;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Domain.Entities;
using Demo.Archivero.Infrastructure.SqlRepository.Data;

namespace Demo.Archivero.Infrastructure.SqlRepository.Repositories;

public sealed class UserRepository(
    Data.DbContextArchivero db)
    : IUserRepository
{
    public async Task<AppUser?> GetUserAsync(string username, CancellationToken ct)
    {
        return await db.AppUsers
            .AsNoTracking()
            .Where(x => x.IsActive && x.Username == username)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        CancellationToken ct)
    {
        var users = await db.AppUsers
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.Role,
                x.IsActive
            })
            .ToListAsync(ct);

        return users
            .Select(x => new UserDto(
                x.Id,
                x.Username,
                x.Role,
                x.IsActive
                ))
            .ToList();
    }

    public async Task CreateUserAsync(
        CreateUserDto dto,
        CancellationToken ct)
    {
        if (await db.AppUsers.AnyAsync(
                x => x.Username == dto.Username,
                ct))
        {
            throw new InvalidOperationException(
                "User already exists.");
        }

   
        var user = new AppUser
        {
            Username = dto.Username.Trim(),
            PasswordHash =
                PasswordHasher.Hash(dto.Password),
            Role = AppPermissionCatalog.NormalizeRole(dto.Role),
           
            IsActive = true
        };

        db.AppUsers.Add(user);

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateUserAsync(
        int id,
        UpdateUserDto dto,
        CancellationToken ct)
    {
        var user = await db.AppUsers
            .FirstOrDefaultAsync(
                x => x.Id == id,
                ct);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User not found.");
        }

        user.Role = AppPermissionCatalog.NormalizeRole(dto.Role);
        user.IsActive = dto.IsActive;
      
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(
        int id,
        string password,
        CancellationToken ct)
    {
        var user = await db.AppUsers
            .FirstOrDefaultAsync(
                x => x.Id == id,
                ct);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User not found.");
        }

        user.PasswordHash =
            PasswordHasher.Hash(password);

        await db.SaveChangesAsync(ct);
    }
}
