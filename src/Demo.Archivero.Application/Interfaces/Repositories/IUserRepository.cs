
using Demo.Archivero.Application.Dtos.User;
using Demo.Archivero.Domain.Entities;

namespace Demo.Archivero.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetUserAsync(string username, CancellationToken ct);

    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct);

    Task CreateUserAsync(CreateUserDto dto, CancellationToken ct);

    Task UpdateUserAsync(int id, UpdateUserDto dto, CancellationToken ct);

    Task ResetPasswordAsync(int id, string password, CancellationToken ct);
}