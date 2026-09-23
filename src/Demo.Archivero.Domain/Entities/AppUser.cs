using Demo.Archivero.Domain.Entities.Bases;

namespace Demo.Archivero.Domain.Entities;

public sealed class AppUser: EntityBase
{
    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public string Role { get; set; } = "Viewer";

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAtUtc { get; set; }
}
