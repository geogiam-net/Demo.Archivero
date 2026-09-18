namespace Demo.Archivero.Application.Security;

public class JwtSettings
{
    public const string ArchiveroJwtConfiguration = "ArchiveroJwtConfiguration";

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public required string SigningKey { get; set; }

    public required int? ExpirationMinutes { get; set; }
}
