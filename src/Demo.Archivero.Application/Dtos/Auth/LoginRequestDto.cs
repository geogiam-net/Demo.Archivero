namespace Demo.Archivero.Application.Dtos.Auth;

public sealed record LoginRequestDto(
    string Username,
    string Password);
