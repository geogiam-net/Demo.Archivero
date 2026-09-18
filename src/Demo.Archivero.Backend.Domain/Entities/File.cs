using Demo.Archivero.Domain.Entities.Bases;
using Demo.Archivero.Domain.Enums;

namespace Demo.Archivero.Domain.Entities;

public sealed class File: EntityBase
{
    public string Title { get; set; } = "";

    public FileState State { get; set; } = FileState.None;

    public string BlobId { get; set; } = "";
}
