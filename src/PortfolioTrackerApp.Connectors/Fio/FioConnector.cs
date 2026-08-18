using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Domain.Holdings;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Connectors.Fio;

/// <summary>
/// Fio banka: personal API token, one token = one account. Balance is read from the
/// `periods` export header (idempotent; `last` would move a server-side bookmark).
/// The token travels in the URL path — no code in this class may log the request URI.
/// </summary>
internal sealed class FioConnector(HttpClient httpClient, TimeProvider timeProvider) : IConnector
{
    private const string Source = "fio";

    public string SourceId => Source;

    public async Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken)
    {
        var token = credential.Trim();
        if (token.Length == 0)
            return ConnectorSyncResult.Failed(Source, SyncStatus.InvalidCredential);

        // Balance-only sync: a 1-day window is enough, closingBalance always comes in `info`.
        var today = timeProvider.GetUtcNow().UtcDateTime;
        var url = $"v1/rest/periods/{token}/{today:yyyy-MM-dd}/{today:yyyy-MM-dd}/transactions.json";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ConnectorSyncResult.Failed(Source, SyncStatus.Unavailable);
        }

        if (!response.IsSuccessStatusCode)
            return ConnectorSyncResult.Failed(Source, MapStatus(response.StatusCode));

        try
        {
            var export = await response.Content.ReadFromJsonAsync<FioExport>(cancellationToken);
            var info = export?.AccountStatement?.Info
                ?? throw new JsonException("Missing accountStatement.info.");

            // Promote into the domain: constructors enforce the invariants (currency shape,
            // amount/currency coupling). Nonsense from the bank throws here, at the boundary.
            var balance = new Money(info.ClosingBalance, info.Currency ?? "");
            var position = Position.OfCash(balance, Source, timeProvider.GetUtcNow());

            return new ConnectorSyncResult
            {
                Source = Source,
                Status = SyncStatus.Ok,
                AccountLabel = info.Iban,
                AsOf = position.AsOf,
                Holdings =
                [
                    new SyncedHolding
                    {
                        Kind = "cash",
                        Symbol = position.Asset.Symbol,
                        Quantity = position.Quantity,
                        Currency = position.Asset.QuoteCurrency,
                    },
                ],
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // Bank sent data we can't trust — per design decision, same user value as an outage.
            return ConnectorSyncResult.Failed(Source, SyncStatus.Unavailable);
        }
    }

    private static SyncStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        // Fio's documented semantics (spec section 8): 500 = invalid/inactive token(!), 409 = rate limit.
        HttpStatusCode.InternalServerError => SyncStatus.InvalidCredential,
        HttpStatusCode.Conflict => SyncStatus.RateLimited,
        _ => SyncStatus.Unavailable,
    };
}
