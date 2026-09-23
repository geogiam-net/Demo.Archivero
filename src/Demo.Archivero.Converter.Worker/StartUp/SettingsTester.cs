using Demo.Archivero.Converter.Worker.Startup;
using Demo.Archivero.Infrastructure.AzureBlob;

namespace Demo.Archivero.Converter.Worker.StartUp;

internal static class SettingsTester
{
    // At start of application, we read all settings required, if any is missing then we throw an exception.
    public static void TestSettingsExist(IConfiguration configuration)
    {
        RequireConnectionString(configuration, DependencyInjection.DatabaseConnectionName);
        TestArchiveroBlobConfiguration(configuration);
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

    private static void RequireConnectionString(IConfiguration configuration, string name)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(name)))
            throw new InvalidOperationException($"Connection string for '{name}' is missing.");
    }
}