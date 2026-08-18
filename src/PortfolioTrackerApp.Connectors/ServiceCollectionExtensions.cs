using Microsoft.Extensions.DependencyInjection;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Fio;

namespace PortfolioTrackerApp.Connectors;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConnectorsModule(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddHttpClient<IConnector, FioConnector>(client =>
            {
                client.BaseAddress = new Uri("https://fioapi.fio.cz/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            // Law: credentials are never logged. Fio puts the token in the URL path and the
            // default HttpClient logger writes request URIs — so no loggers at all here.
            .RemoveAllLoggers();

        return services;
    }
}
