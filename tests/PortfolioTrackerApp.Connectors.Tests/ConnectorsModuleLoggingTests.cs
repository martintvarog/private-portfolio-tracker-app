using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Fio;
using PortfolioTrackerApp.Connectors.Logging;

namespace PortfolioTrackerApp.Connectors.Tests;

/// <summary>
/// Goes through the real DI wiring (AddConnectorsModule) with a stubbed Fio HTTP response and
/// captures EVERY log line from EVERY category. Guards the two laws end to end:
/// the outbound HttpClient pipeline is silent (no URL with the token), and the only
/// connector log line is the decorator's outcome line.
/// </summary>
public class ConnectorsModuleLoggingTests
{
    private const string Token = "SECRET-TOKEN-abc123";
    private const string Iban = "CZ7920100000002400222222";
    private const string FioBody = """
        {"accountStatement":{"info":{"accountId":"2400222222","bankId":"2010","currency":"CZK",
        "iban":"CZ7920100000002400222222","bic":"FIOBCZPPXXX","openingBalance":195.00,"closingBalance":195.01},
        "transactionList":{"transaction":[]}}}
        """;

    private static async Task<(ConnectorSyncResult Result, IReadOnlyList<(string Category, LogLevel Level, string Message)> Lines)> SyncViaModule(HttpStatusCode fioStatus)
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs)); // capture everything, even Trace
        services.AddConnectorsModule();
        // Swap the network for a stub. The typed client is named after its type.
        services.AddHttpClient<FioConnector>()
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(fioStatus, FioBody));

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConnector>();
        var result = await connector.FetchHoldingsAsync(Token, CancellationToken.None);
        return (result, logs.Lines.ToList());
    }

    [Fact]
    public async Task IConnector_resolves_to_the_logging_decorator_around_fio()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConnectorsModule();
        await using var provider = services.BuildServiceProvider();

        var connector = provider.GetRequiredService<IConnector>();

        Assert.IsType<LoggingConnector>(connector);
        Assert.Equal("fio", connector.SourceId);
    }

    [Fact]
    public async Task Successful_sync_produces_exactly_one_connector_log_line_and_no_secrets_anywhere()
    {
        var (result, lines) = await SyncViaModule(HttpStatusCode.OK);

        Assert.Equal(SyncStatus.Ok, result.Status);
        Assert.Equal(Iban, result.AccountLabel); // the DATA still flows to the caller...

        var connectorLines = lines.Where(l => l.Category.StartsWith("PortfolioTrackerApp.")).ToList();
        var line = Assert.Single(connectorLines);
        Assert.Equal(LogLevel.Information, line.Level);

        foreach (var (category, _, message) in lines) // ...but no log line, in ANY category, may carry it
        {
            Assert.DoesNotContain(Token, message);
            Assert.DoesNotContain(Iban, message);
            Assert.DoesNotContain("fioapi.fio.cz", message);
            Assert.DoesNotContain("/v1/rest/", message);
            Assert.DoesNotContain("closingBalance", message);
            Assert.False(category.StartsWith("System.Net.Http.HttpClient"), $"HttpClient logging must be removed, got: {category}: {message}");
        }
    }

    [Fact]
    public async Task Failed_sync_is_a_warning_without_the_token()
    {
        var (result, lines) = await SyncViaModule(HttpStatusCode.Conflict);

        Assert.Equal(SyncStatus.RateLimited, result.Status);
        var line = Assert.Single(lines, l => l.Category.StartsWith("PortfolioTrackerApp."));
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains("RateLimited", line.Message);
        Assert.All(lines, l => Assert.DoesNotContain(Token, l.Message));
    }

    /// <summary>Walks the handler chain the factory builds for a named client — public API, no reflection.</summary>
    private static IEnumerable<HttpMessageHandler> HandlerChain(IServiceProvider provider, string clientName)
    {
        HttpMessageHandler? handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        while (handler is not null)
        {
            yield return handler;
            handler = (handler as DelegatingHandler)?.InnerHandler;
        }
    }

    private static bool IsFactoryLoggingHandler(HttpMessageHandler h) =>
        h is LoggingHttpMessageHandler or LoggingScopeHttpMessageHandler;

    [Fact]
    public async Task Fio_http_client_registration_has_default_logging_removed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConnectorsModule();
        await using var provider = services.BuildServiceProvider();

        // Typed clients are named after their type. RemoveAllLoggers() must leave NO logging handler in the chain.
        var chain = HandlerChain(provider, nameof(FioConnector)).ToList();

        Assert.NotEmpty(chain);
        Assert.DoesNotContain(chain, IsFactoryLoggingHandler);
    }

    [Fact]
    public async Task Without_RemoveAllLoggers_the_http_client_WOULD_log_the_token_url()
    {
        // Control experiment: proves the leak is real and that the guard test above is sensitive to it.
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<FioConnector>(c => c.BaseAddress = new Uri("https://fioapi.fio.cz/"))
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(HttpStatusCode.OK, FioBody));
        // deliberately NO .RemoveAllLoggers()
        await using var provider = services.BuildServiceProvider();

        Assert.Contains(HandlerChain(provider, nameof(FioConnector)), IsFactoryLoggingHandler);
        await provider.GetRequiredService<FioConnector>().FetchHoldingsAsync(Token, CancellationToken.None);

        Assert.Contains(logs.Lines, l => l.Category.StartsWith("System.Net.Http.HttpClient") && l.Message.Contains(Token));
    }
}
