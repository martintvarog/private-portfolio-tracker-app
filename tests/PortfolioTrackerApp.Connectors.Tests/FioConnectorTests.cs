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
    public async Task Valid_response_yields_one_cash_holding_with_iban_label()
    {
        var result = await Fetch(HttpStatusCode.OK, ValidBody);

        Assert.Equal(SyncStatus.Ok, result.Status);
        Assert.Equal("fio", result.Source);
        Assert.Equal("CZ7920100000002400222222", result.AccountLabel);
        var holding = Assert.Single(result.Holdings);
        Assert.Equal("cash", holding.Kind);
        Assert.Equal("CZK", holding.Symbol);
        Assert.Equal("CZK", holding.Currency);
        Assert.Equal(195.01m, holding.Quantity);
    }

    [Fact]
    public async Task Http_500_means_invalid_or_inactive_token_per_fio_spec()
    {
        var result = await Fetch(HttpStatusCode.InternalServerError, "");

        Assert.Equal(SyncStatus.InvalidCredential, result.Status);
        Assert.Empty(result.Holdings);
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

    private static async Task<ConnectorSyncResult> Fetch(
        HttpStatusCode statusCode, string body, string credential = "sometoken")
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(statusCode, body))
        {
            BaseAddress = new Uri("https://fioapi.fio.cz/"),
        };
        var connector = new FioConnector(httpClient, TimeProvider.System);

        return await connector.FetchHoldingsAsync(credential, CancellationToken.None);
    }
}
