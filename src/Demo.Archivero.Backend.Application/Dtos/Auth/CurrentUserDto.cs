namespace Demo.Archivero.Application.Dtos.Auth;

public sealed record CurrentUserDto(
    string Username,
    string Role,
    IReadOnlyList<string> Permissions);