using Demo.Archivero.Converter.Worker.Services;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Application.Services;
using Demo.Archivero.Infrastructure.AzureBlob;
using Demo.Archivero.Infrastructure.OpenXml;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Demo.Archivero.Infrastructure.SqlRepository.Repositories;
using Microsoft.EntityFrameworkCore;
using Demo.Archivero.Infrastructure.ServiceBus;
using Demo.Archivero.Converter.Worker.Workers;


namespace Demo.Archivero.Converter.Worker.Startup;

public static class DependencyInjection
{
    public static IServiceCollection AddFileWorkers(this IServiceCollection services)
    {
        services.AddScoped<IWordFileMessageHandler, WordFileMessageHandler>();
        services.AddSingleton<CreateWordFileReceiver>();
        services.AddSingleton<DeleteWordFileReceiver>();
        services.AddHostedService<CreateWordFileWorker>();
        services.AddHostedService<DeleteWordFileWorker>();
        return services;
    }

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
        services.AddScoped<IWordFileService, WordFileService>();

        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IOpenXmlWordService, OpenXmlWordService>();
        services.AddScoped<IBlobService, BlobService>();

        return services;
    }

}

