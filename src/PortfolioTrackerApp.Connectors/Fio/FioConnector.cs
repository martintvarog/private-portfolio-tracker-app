using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Domain.Positions;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Connectors.Fio;

internal sealed class FioConnector(HttpClient httpClient, TimeProvider timeProvider) : IConnector
{
    private const string Source = "fio";
    private const HttpStatusCode InvalidOrInactiveToken = HttpStatusCode.InternalServerError;
    private const HttpStatusCode TokenRateLimited = HttpStatusCode.Conflict;

    public string SourceId => Source;

    public async Task<ConnectorSyncResult> FetchPositionsAsync(string credential, CancellationToken cancellationToken)
    {
        var token = credential.Trim();
        if (token.Length == 0)
            return ConnectorSyncResult.Failed(Source, SyncStatus.InvalidCredential);

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
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
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

            var balance = new Money(info.ClosingBalance, info.Currency ?? "");
            var position = Position.OfCash(balance, Source, timeProvider.GetUtcNow());

            return new ConnectorSyncResult
            {
                Source = Source,
                Status = SyncStatus.Ok,
                AccountLabel = info.Iban,
                AsOf = position.AsOf,
                Positions =
                [
                    new SyncedPosition
                    {
                        AssetType = "cash",
                        Symbol = position.Asset.Symbol,
                        Quantity = position.Quantity,
                        Currency = position.Asset.QuoteCurrency,
                    },
                ],
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return ConnectorSyncResult.Failed(Source, SyncStatus.Unavailable);
        }
    }

    private static SyncStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        InvalidOrInactiveToken => SyncStatus.InvalidCredential,
        TokenRateLimited => SyncStatus.RateLimited,
        _ => SyncStatus.Unavailable,
    };
}
