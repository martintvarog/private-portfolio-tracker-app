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

        // Fio: registered as ITSELF (typed HttpClient), then exposed as IConnector through the
        // logging decorator below. Connectors never see an ILogger.
        services.AddHttpClient<FioConnector>(client =>
            {
                client.BaseAddress = new Uri("https://fioapi.fio.cz/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            // Law: credentials are never logged. Fio puts the token in the URL path and the
            // default HttpClient logger writes request URIs — so no loggers at all here.
            // Guarded by ConnectorsModuleLoggingTests.
            .RemoveAllLoggers();
        services.AddConnector<FioConnector>();

        return services;
    }

    /// <summary>Exposes a registered connector as <see cref="IConnector"/>, wrapped in <see cref="LoggingConnector"/>.</summary>
    private static IServiceCollection AddConnector<TConnector>(this IServiceCollection services)
        where TConnector : class, IConnector =>
        services.AddTransient<IConnector>(sp => new LoggingConnector(
            sp.GetRequiredService<TConnector>(),
            sp.GetRequiredService<ILogger<LoggingConnector>>(),
            sp.GetRequiredService<TimeProvider>()));
}
