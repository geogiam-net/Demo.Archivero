namespace Demo.Archivero.Application.Dtos.File;

public sealed record FileDto(
    string Title,
    DateTime CreatedAtUtc,
    string BlobUrl);