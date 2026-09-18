using Microsoft.EntityFrameworkCore;
using Slg.DeadKm.Api.Services;
using Slg.DeadKm.Api.Workers;
using Slg.DeadKm.Application.Interfaces;
using Slg.DeadKm.Application.Interfaces.Application;
using Slg.DeadKm.Application.Interfaces.Infrastructure;
using Slg.DeadKm.Application.Interfaces.Repositories;
using Slg.DeadKm.Application.Services;
using Slg.DeadKm.Application.Settings;
using Slg.DeadKm.Infrastructure.DataClients.GeoPortal;
using Slg.DeadKm.Infrastructure.DataClients.IvuDataSource;
using Slg.DeadKm.Infrastructure.DataClients.TomTom;
using Slg.DeadKm.Infrastructure.DataClients.WebFleetBlob;
using Slg.DeadKm.Infrastructure.SqlRepository.Data;
using Slg.DeadKm.Infrastructure.SqlRepository.Repositories;
using Slg.DeadKm.Infrastructure.SqlRepository.Services;

namespace Slg.DeadKm.Api.Startup;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "DeadKmSqlServer";

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        services.AddDbContext<DeadKmDbContext>(options => 
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleShiftRepository, VehicleShiftRepository>();
        services.AddScoped<IGeoPortalLineRepository, GeoPortalLineRepository>();
        services.AddScoped<IWebFleetTrackingRepository, WebFleetTrackingRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IRouteFinder, TomTomRoutingService>();
        services.AddScoped<IVehicleShiftService, VehicleShiftService>();
        services.AddScoped<IIvuDataSourceService, IvuDataSourceService>();

        // Fixed algorithm thresholds - single hardcoded instance, not bound to configuration.
        services.AddSingleton<IVehicleShiftSettings, VehicleShiftSettings>();

        // For Workers: Import services are keyed by data source; resolve with [FromKeyedServices(ImportServiceKeys.X)].
        services.AddKeyedScoped<IImportService, GeoPortalImportService>(ImportServiceKeys.GeoPortal);
        services.AddKeyedScoped<IImportService, WebFleetBlobImportService>(ImportServiceKeys.WebFleet);

        return services;
    }

    public static IServiceCollection AddWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkerSettings>(configuration.GetSection(WorkerSettings.SectionName));

        services.AddHostedService<GeoPortalImportWorker>();
        services.AddHostedService<WebFleetBlobImportWorker>();
        services.AddHostedService<VehicleShiftPrecalculationWorker>();

        return services;
    }
}
