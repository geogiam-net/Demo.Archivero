using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.Auth;

namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IAuthService
{
    Task<ResultDto<LoginResponseDto?>> LoginAsync(string username, string password, CancellationToken ct);
}