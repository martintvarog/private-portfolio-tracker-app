using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Fio;
using PortfolioTrackerApp.Connectors.Logging;

namespace PortfolioTrackerApp.Connectors;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConnectorsModule(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddConnector<FioConnector>(client =>
        {
            client.BaseAddress = new Uri("https://fioapi.fio.cz/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// The ONLY way to register a connector. One typed HttpClient per connector, configured by the caller;
    /// the connector is exposed as <see cref="IConnector"/> wrapped in <see cref="LoggingConnector"/>.
    /// Law, enforced by construction rather than by remembering: the HttpClient has NO loggers —
    /// connectors put credentials in URLs (Fio: token in the path) and the default HttpClient logger
    /// writes request URIs. Guarded by ConnectorsModuleLoggingTests for every registered connector.
    /// </summary>
    private static IServiceCollection AddConnector<TConnector>(
        this IServiceCollection services, Action<HttpClient> configureClient)
        where TConnector : class, IConnector
    {
        services.AddHttpClient<TConnector>(configureClient)
            .RemoveAllLoggers();

        return services.AddTransient<IConnector>(sp => new LoggingConnector(
            sp.GetRequiredService<TConnector>(),
            sp.GetRequiredService<ILogger<LoggingConnector>>(),
            sp.GetRequiredService<TimeProvider>()));
    }
}
