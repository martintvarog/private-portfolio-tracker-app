namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// Fetches current positions from one source with a source-specific credential.
/// Every implementation must obey the connector laws in <c>docs/integrations/README.md</c>.
/// </summary>
public interface IConnector
{
    /// <summary>Stable id clients send as the sync source, e.g. "fio".</summary>
    string SourceId { get; }

    Task<ConnectorSyncResult> FetchPositionsAsync(string credential, CancellationToken cancellationToken);
}
