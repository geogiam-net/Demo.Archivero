using Azure.Messaging.ServiceBus;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Demo.Archivero.Infrastructure.ServiceBus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchiveroFileBus(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(FileBusSettings.FileBusConfiguration).Get<FileBusSettings>()
            ?? throw new InvalidOperationException($"Missing {FileBusSettings.FileBusConfiguration} configuration.");

        settings.Validate();

        services.TryAddSingleton(settings);
        services.TryAddSingleton(provider => new ServiceBusClient(
            provider.GetRequiredService<FileBusSettings>().ConnectionString,
            new ServiceBusClientOptions
            {
                RetryOptions = new ServiceBusRetryOptions
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 5,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(30)
                }
            }));

        services.TryAddSingleton<ICreateWordFileSender, CreateWordFileSender>();
        services.TryAddSingleton<IDeleteWordFileSender, DeleteWordFileSender>();

        return services;
    }

    // Call AddArchiveroFileBus first. Only consuming hosts should register this hosted service.
    public static IServiceCollection AddArchiveroFileBusReceiver<THandler>(this IServiceCollection services)
        where THandler : class, IWordFileMessageHandler
    {
        services.AddScoped<IWordFileMessageHandler, THandler>();
        services.AddHostedService<CreateWordFileReceiver>();
        services.AddHostedService<DeleteWordFileReceiver>();
        return services;
    }
}

