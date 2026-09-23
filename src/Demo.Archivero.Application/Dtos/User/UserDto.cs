namespace Demo.Archivero.Application.Dtos.User;

public sealed record UserDto(
    int Id,
    string Username,
    string Role,
    bool IsActive);