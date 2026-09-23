namespace Demo.Archivero.Application.Dtos.File;

public sealed record FileDto(
    int FileId,
    string Title,
    DateTime CreatedAtUtc,
    string BlobUrl);