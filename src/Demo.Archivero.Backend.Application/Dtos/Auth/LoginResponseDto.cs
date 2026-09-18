namespace Demo.Archivero.Application.Dtos.Auth;

public sealed record LoginResponseDto(
    string Token,
    string Username,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTime ExpiresAtUtc);