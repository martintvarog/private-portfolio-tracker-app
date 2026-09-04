using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Logging;

namespace PortfolioTrackerApp.Connectors.Tests;

public class LoggingConnectorTests
{
    private const string Credential = "SECRET-TOKEN-abc123";
    private const string Iban = "CZ7920100000002400222222";

    private sealed class FakeConnector(ConnectorSyncResult result) : IConnector
    {
        public string SourceId => "fake";
        public string? SeenCredential { get; private set; }
        public Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken)
        {
            SeenCredential = credential;
            return Task.FromResult(result);
        }
    }

    private static ConnectorSyncResult OkResult() => new()
    {
        Source = "fake",
        Status = SyncStatus.Ok,
        AccountLabel = Iban,
        Holdings = [new SyncedHolding { Kind = "cash", Symbol = "CZK", Quantity = 195.01m, Currency = "CZK" }],
    };

    private static async Task<(ConnectorSyncResult Result, CapturingLoggerProvider Logs)> Run(ConnectorSyncResult inner)
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var fake = new FakeConnector(inner);
        var sut = new LoggingConnector(fake, factory.CreateLogger<LoggingConnector>(), TimeProvider.System);

        var result = await sut.FetchHoldingsAsync(Credential, CancellationToken.None);

        Assert.Equal(Credential, fake.SeenCredential); // decorator passes the credential through untouched
        Assert.Same(inner, result);                     // and returns the inner result as-is
        return (result, logs);
    }

    [Fact]
    public async Task Ok_outcome_is_logged_once_at_information_with_source_and_status()
    {
        var (_, logs) = await Run(OkResult());

        var line = Assert.Single(logs.Lines);
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains("fake", line.Message);
        Assert.Contains("Ok", line.Message);
        Assert.Matches(@"\d+ ms", line.Message);
    }

    [Theory]
    [InlineData(SyncStatus.InvalidCredential)]
    [InlineData(SyncStatus.Unavailable)]
    [InlineData(SyncStatus.RateLimited)]
    public async Task Non_ok_outcome_is_logged_at_warning(SyncStatus status)
    {
        var (_, logs) = await Run(ConnectorSyncResult.Failed("fake", status));

        var line = Assert.Single(logs.Lines);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(status.ToString(), line.Message);
    }

    [Fact]
    public async Task Log_line_never_contains_credential_account_label_or_holdings()
    {
        var (_, logs) = await Run(OkResult());

        var line = Assert.Single(logs.Lines);
        Assert.DoesNotContain(Credential, line.Message);
        Assert.DoesNotContain(Iban, line.Message);
        Assert.DoesNotContain("195", line.Message);   // balance
        Assert.DoesNotContain("CZK", line.Message);   // holding detail
    }
}
