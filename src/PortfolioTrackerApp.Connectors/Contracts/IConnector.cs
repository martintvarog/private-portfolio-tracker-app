namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// A connector fetches current holdings from one external institution (bank, broker, chain).
///
/// Laws every implementation must obey (enforced by the shared contract-test suite):
/// <list type="bullet">
/// <item>Outcomes are data: business failures (expired credential, institution down,
/// rate limit) are returned as <see cref="ConnectorSyncResult.Status"/>, never thrown.
/// Exceptions are reserved for programming errors and cancellation.</item>
/// <item>Idempotent: the same call twice returns the same holdings and mutates no
/// state anywhere — ours or the institution's (e.g. Fio: use `periods`, never `last`).</item>
/// <item>Source-tagged: every result carries <see cref="SourceId"/> so the client vault
/// knows which stored positions to replace.</item>
/// <item>Credentials pass through: never persisted, never logged — including inside
/// request URLs (Fio puts the token in the URL path) and inside EXCEPTION MESSAGES:
/// the framework logs unhandled exceptions verbatim, so a credential in a message
/// is a credential in the logs.</item>
/// <item>Unsupported instruments are skipped loudly via <see cref="ConnectorSyncResult.Warnings"/>,
/// never silently dropped.</item>
/// </list>
/// </summary>
public interface IConnector
{
    /// <summary>Stable identifier of the institution, e.g. "fio", "ibkr", "crypto".</summary>
    string SourceId { get; }

    /// <summary>
    /// Fetches current holdings using the supplied credential (Fio: personal API token;
    /// crypto: public address). The credential's shape is connector-specific.
    /// </summary>
    Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken);
}
