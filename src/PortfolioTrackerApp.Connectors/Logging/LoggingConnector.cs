using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;

namespace PortfolioTrackerApp.Connectors.Logging;

internal sealed class LoggingConnector(IConnector inner, ILogger<LoggingConnector> logger, TimeProvider timeProvider) : IConnector
{
    public string SourceId => inner.SourceId;

    internal Type InnerType => inner.GetType();

    public async Task<ConnectorSyncResult> FetchPositionsAsync(string credential, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var result = await inner.FetchPositionsAsync(credential, cancellationToken);
        var elapsedMs = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;

        var level = result.Status == SyncStatus.Ok ? LogLevel.Information : LogLevel.Warning;
        logger.Log(level, "Sync {Source} finished with {Status} in {ElapsedMs} ms",
            result.Source, result.Status, elapsedMs);

        return result;
    }
}
