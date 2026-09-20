using Microsoft.Extensions.DependencyInjection;

namespace Demo.Archivero.Converter.Client;

public static class ConverterApiClientServiceCollectionExtensions
{
    public static IServiceCollection AddConverterApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddHttpClient<IConverterApiClient, ConverterApiClient>(client => client.BaseAddress = baseAddress);
        return services;
    }
}
