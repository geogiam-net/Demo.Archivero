using Demo.Archivero.Domain.Entities.Bases;
using Demo.Archivero.Domain.Enums;

namespace Demo.Archivero.Domain.Entities;

public sealed class File: EntityBase
{
    public int OwnerId { get; set; }

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    public FileStatus Status { get; set; } = FileStatus.None;

    public string BlobId { get; set; } = "";
}
