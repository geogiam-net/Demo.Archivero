using Slg.DeadKm.Api.Startup;
using Slg.DeadKm.Api.Workers;
using Slg.DeadKm.Infrastructure.DataClients.IvuDataSource;
using Slg.DeadKm.Infrastructure.DataClients.TomTom;
using Slg.DeadKm.Infrastructure.DataClients.WebFleetBlob;

namespace Slg.DeadKm.Api.StartUp;

internal static class SettingsTester
{
    // At start of application, we read all settings required, if any is missing then we throw an exception.
    public static void TestSettingsExist(IConfiguration configuration)
    {
        RequireConnectionString(configuration, DependencyInjection.DatabaseConnectionName);

        RequireValue(configuration, "DeadKmAuth:AdminUsername");
        RequireValue(configuration, "DeadKmAuth:AdminPassword");

        TestDeadKmJwtConfiguration(configuration);
        TestTomTomConfiguration(configuration);
        TestBlobStorageConfiguration(configuration);
        TestWorkersConfiguration(configuration);
        TestIvuDataSourceConfiguration(configuration);
    }

    private static void TestIvuDataSourceConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(IvuDataSourceSettings.IvuDataSourceConfiguration).Get<IvuDataSourceSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"IvuDataSource settings with name '{IvuDataSourceSettings.IvuDataSourceConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing ClientId.");
        }

        if (string.IsNullOrWhiteSpace(settings.TenantId))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing TenantId.");
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing Password.");
        }

        if (string.IsNullOrWhiteSpace(settings.SqlEndpoints))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing SqlEndpoints.");
        }

        if (string.IsNullOrWhiteSpace(settings.Database))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing Database.");
        }

        if (string.IsNullOrWhiteSpace(settings.TableName))
        {
            throw new InvalidOperationException("IvuDataSource settings are missing TableName.");
        }
    }

    private static void TestWorkersConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(WorkerSettings.SectionName).Get<WorkerSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"Workers settings with name '{WorkerSettings.SectionName}' not found");
        }
    }

    private static void TestTomTomConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(TomTomSettings.TomTomConfiguration).Get<TomTomSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"TomTom settings with name '{TomTomSettings.TomTomConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("TomTom settings are missing ApiKey.");
        }

        if (string.IsNullOrWhiteSpace(settings.RoutingApiUrl))
        {
            throw new InvalidOperationException("TomTom settings are missing RoutingApiUrl.");
        }
    }

    private static void TestDeadKmJwtConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(DeadKmJwtSettings.DeadKmJwtConfiguration).Get<DeadKmJwtSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"DeadKmJwt settings with name '{DeadKmJwtSettings.DeadKmJwtConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException("DeadKmJwt settings are missing SigningKey.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException("DeadKmJwt settings are missing Issuer.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("DeadKmJwt settings are missing Audience.");
        }
    }

    private static void TestBlobStorageConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(BlobStorageSettings.BlobConfiguration).Get<BlobStorageSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException($"BlobStorage settings with name '{BlobStorageSettings.BlobConfiguration}' not found");
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("BlobStorage settings are missing ConnectionString.");
        }

        if (string.IsNullOrWhiteSpace(settings.BackupsContainerName))
        {
            throw new InvalidOperationException("BlobStorage settings are missing BackupsContainerName.");
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
