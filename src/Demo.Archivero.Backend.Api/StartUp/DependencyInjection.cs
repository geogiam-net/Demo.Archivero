using Microsoft.EntityFrameworkCore;
using Demo.Archivero.Api.Services;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Infrastructure.SqlRepository.Services;
using Demo.Archivero.Infrastructure.SqlRepository.Repositories;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Backend.Infrastructure.OpenXml;
using Demo.Archivero.Backend.Infrastructure.AzureBlob;


namespace Demo.Archivero.Api.Startup;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "ArchiveroDB";

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        services.AddDbContext<DbContextArchivero>(options => 
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

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IOpenXmlWordService, OpenXmlWordService>();
        services.AddScoped<IBlobService, BlobService>();

        return services;
    }

}
