namespace Demo.Archivero.Application.Dtos.File;

public sealed record CreateWordFileRequestDto(
    int FileId,
    string Username);