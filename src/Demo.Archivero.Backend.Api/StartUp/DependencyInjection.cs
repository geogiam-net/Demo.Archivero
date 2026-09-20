using Demo.Archivero.Api.Services;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Application.Services;
using Demo.Archivero.Backend.Infrastructure.AzureBlob;
using Demo.Archivero.Backend.Infrastructure.OpenXml;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Demo.Archivero.Infrastructure.SqlRepository.Repositories;
using Demo.Archivero.Infrastructure.SqlRepository.Services;
using Microsoft.EntityFrameworkCore;


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
        services.AddScoped<IFileRepository, FileRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFileService, FileService>();

        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();

        return services;
    }

}
