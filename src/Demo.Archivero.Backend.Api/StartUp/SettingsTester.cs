using Demo.Archivero.Api.Startup;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Backend.Infrastructure.AzureBlob;

namespace Demo.Archivero.Api.StartUp;

internal static class SettingsTester
{
    // At start of application, we read all settings required, if any is missing then we throw an exception.
    public static void TestSettingsExist(IConfiguration configuration)
    {
        RequireConnectionString(configuration, DependencyInjection.DatabaseConnectionName);

        RequireValue(configuration, "ArchiveroAuth:AdminUsername");
        RequireValue(configuration, "ArchiveroAuth:AdminPassword");

        TestArchiveroJwtConfiguration(configuration);
        TestArchiveroBlobConfiguration(configuration);
    }

    private static void TestArchiveroJwtConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.ArchiveroJwtConfiguration).Get<JwtSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"ArchiveroJwt settings with name '{JwtSettings.ArchiveroJwtConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException("ArchiveroJwt settings are missing SigningKey.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException("ArchiveroJwt settings are missing Issuer.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("ArchiveroJwt settings are missing Audience.");
        }
    }

    private static void TestArchiveroBlobConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(BlobServiceSettings.BlobServiceConfiguration).Get<BlobServiceSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"BlobService settings with name '{BlobServiceSettings.BlobServiceConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("BlobService settings are missing ConnectionString.");
        }

        if (string.IsNullOrWhiteSpace(settings.ContainerName))
        {
            throw new InvalidOperationException("BlobService settings are missing ContainerName.");
        }
    }

    private static void RequireValue(IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
            throw new InvalidOperationException($"Configuration value for '{key}' is missing.");
    }

    private static void RequireConnectionString(IConfiguration configuration, string name)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(name)))
            throw new InvalidOperationException($"Connection string for '{name}' is missing.");
    }
}
