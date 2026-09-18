namespace Slg.DeadKm.Api;

public class DeadKmJwtSettings
{
    public const string DeadKmJwtConfiguration = "DeadKmJwt";

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public required string SigningKey { get; set; }

    public required long ExpirationMinutes { get; set; }
}
