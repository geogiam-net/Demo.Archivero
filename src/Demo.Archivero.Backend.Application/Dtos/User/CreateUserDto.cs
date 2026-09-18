namespace Demo.Archivero.Application.Dtos.User;

public sealed record CreateUserDto(
    string Username,
    string Password,
    string Role);