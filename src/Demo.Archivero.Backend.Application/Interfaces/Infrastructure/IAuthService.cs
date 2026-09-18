using Demo.Archivero.Application.Dtos.Auth;

namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(string username, string password, CancellationToken ct);
}