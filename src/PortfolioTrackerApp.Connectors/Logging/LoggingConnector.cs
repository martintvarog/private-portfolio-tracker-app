using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;

namespace PortfolioTrackerApp.Connectors.Logging;

/// <summary>
/// Decorator: logs ONE outcome line per sync for any connector — source, status, duration.
/// This is the only place connector activity is logged, so the "never log credentials,
/// account labels, holdings or URLs" law is enforced here, once, for every connector.
/// </summary>
internal sealed class LoggingConnector(IConnector inner, ILogger<LoggingConnector> logger, TimeProvider timeProvider) : IConnector
{
    public string SourceId => inner.SourceId;

    public async Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var result = await inner.FetchHoldingsAsync(credential, cancellationToken);
        var elapsedMs = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;

        // Ok is routine (Information); anything else is worth noticing/alerting on (Warning).
        // Structured placeholders → queryable fields; NO credential, label, holdings or URL.
        var level = result.Status == SyncStatus.Ok ? LogLevel.Information : LogLevel.Warning;
        logger.Log(level, "Sync {Source} finished with {Status} in {ElapsedMs} ms",
            result.Source, result.Status, elapsedMs);

        return result;
    }
}
