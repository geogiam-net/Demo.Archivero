namespace Demo.Archivero.Application.Dtos.File;

public sealed record CreateWordFileRequestDto(
    string username,
    string Title,
    string Content);