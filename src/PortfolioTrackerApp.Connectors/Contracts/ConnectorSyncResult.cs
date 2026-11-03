namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// Result of one sync. On failure <see cref="Status"/> says why and <see cref="Positions"/> is empty.
/// Personal data: passes through to the client, never stored or logged server-side (ADR-0011).
/// </summary>
public sealed record ConnectorSyncResult
{
    /// <summary>Id of the connector that produced the result, e.g. "fio".</summary>
    public required string Source { get; init; }

    /// <summary>Outcome of the sync; anything but Ok means no positions, e.g. Ok or InvalidCredential.</summary>
    public required SyncStatus Status { get; init; }

    /// <summary>How the source names the account, telling accounts at one source apart. Null on failure, e.g. "CZ7920100000002400222222" (Fio: IBAN).</summary>
    public string? AccountLabel { get; init; }

    /// <summary>Moment the source reported the positions. Null on failure, e.g. 2026-11-03T10:15:00+00:00.</summary>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>Current positions at the source; empty on failure, e.g. one CZK cash position for a Fio account.</summary>
    public IReadOnlyList<SyncedPosition> Positions { get; init; } = [];

    /// <summary>Everything skipped or odd during the sync, as user-readable text; never dropped silently, e.g. "Skipped unsupported instrument XS0123456789".</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static ConnectorSyncResult Failed(string source, SyncStatus status) =>
        new() { Source = source, Status = status };
}
