namespace Demo.Archivero.Application.Dtos.File;

public sealed record CreateFileRequestDto(
    string Title,
    string Content);