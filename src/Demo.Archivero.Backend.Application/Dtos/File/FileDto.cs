namespace Demo.Archivero.Application.Dtos.File;

public sealed record FileDto(
    int id,
    string Title,
    DateTime CreatedAtUtc,
    string BlobUrl);