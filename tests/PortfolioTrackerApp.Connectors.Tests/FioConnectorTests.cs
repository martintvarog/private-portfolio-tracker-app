using System.Diagnostics;
using System.Net;
using System.Text;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Fio;

namespace PortfolioTrackerApp.Connectors.Tests;

public class FioConnectorTests
{
    private const string ValidBody = """
        {"accountStatement":{"info":{
            "accountId":"2400222222","bankId":"2010","currency":"CZK",
            "iban":"CZ7920100000002400222222","bic":"FIOBCZPPXXX",
            "openingBalance":195.00,"closingBalance":195.01},
            "transactionList":{"transaction":[]}}}
        """;

    [Fact]
    public async Task Valid_response_yields_one_cash_position_with_iban_label()
    {
        var result = await Fetch(HttpStatusCode.OK, ValidBody);

        Assert.Equal(SyncStatus.Ok, result.Status);
        Assert.Equal("fio", result.Source);
        Assert.Equal("CZ7920100000002400222222", result.AccountLabel);
        var position = Assert.Single(result.Positions);
        Assert.Equal("cash", position.AssetType);
        Assert.Equal("CZK", position.Symbol);
        Assert.Equal("CZK", position.Currency);
        Assert.Equal(195.01m, position.Quantity);
    }

    [Fact]
    public async Task Http_500_means_invalid_or_inactive_token_per_fio_spec()
    {
        var result = await Fetch(HttpStatusCode.InternalServerError, "");

        Assert.Equal(SyncStatus.InvalidCredential, result.Status);
        Assert.Empty(result.Positions);
    }

    [Fact]
    public async Task Http_409_is_fio_rate_limit()
    {
        var result = await Fetch(HttpStatusCode.Conflict, "");

        Assert.Equal(SyncStatus.RateLimited, result.Status);
    }

    [Fact]
    public async Task Other_http_errors_are_unavailable()
    {
        var result = await Fetch(HttpStatusCode.NotFound, "");

        Assert.Equal(SyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Garbage_body_is_unavailable_not_a_crash()
    {
        var result = await Fetch(HttpStatusCode.OK, "not json at all");

        Assert.Equal(SyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Invalid_currency_from_bank_is_stopped_by_domain_rules()
    {
        var body = ValidBody.Replace("\"CZK\"", "\"C!K\"");

        var result = await Fetch(HttpStatusCode.OK, body);

        Assert.Equal(SyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Empty_credential_is_invalid_credential()
    {
        var result = await Fetch(HttpStatusCode.OK, ValidBody, credential: "   ");

        Assert.Equal(SyncStatus.InvalidCredential, result.Status);
    }

    [Fact]
    public async Task Calls_the_idempotent_periods_export_for_today_never_last()
    {
        HttpRequestMessage? sent = null;
        var handler = new FuncHandler((request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidBody) });
        });
        var today = new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero));

        await Fetch(handler, timeProvider: today);

        Assert.Equal("/v1/rest/periods/sometoken/2026-11-03/2026-11-03/transactions.json", sent!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Network_failure_is_unavailable()
    {
        var handler = new FuncHandler((_, _) => throw new HttpRequestException("connection refused"));

        var result = await Fetch(handler);

        Assert.Equal(SyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Bank_not_answering_before_timeout_is_unavailable()
    {
        var result = await Fetch(NeverAnswers, timeout: TimeSpan.FromMilliseconds(50));

        Assert.Equal(SyncStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_instead_of_becoming_a_status()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetch(NeverAnswers, cancellationToken: cancelled.Token));
    }

    private static FuncHandler NeverAnswers => new(async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        throw new UnreachableException();
    });

    private static Task<ConnectorSyncResult> Fetch(
        HttpStatusCode statusCode, string body, string credential = "sometoken") =>
        Fetch(new StubHttpMessageHandler(statusCode, body), credential);

    private static async Task<ConnectorSyncResult> Fetch(
        HttpMessageHandler handler,
        string credential = "sometoken",
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fioapi.fio.cz/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
        var connector = new FioConnector(httpClient, timeProvider ?? TimeProvider.System);

        return await connector.FetchPositionsAsync(credential, cancellationToken);
    }

    private sealed class FuncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
