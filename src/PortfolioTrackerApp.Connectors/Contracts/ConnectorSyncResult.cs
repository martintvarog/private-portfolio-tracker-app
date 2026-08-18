namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// Outcome of one connector sync. Always usable: on failure, <see cref="Status"/>
/// says why and <see cref="Holdings"/> is empty — one dead connector must never
/// poison the others' results. Contains personal data (account label, holdings):
/// transits to the client vault, is never persisted or logged server-side.
/// </summary>
public sealed record ConnectorSyncResult
{
    /// <summary>Which connector produced this, e.g. "fio".</summary>
    public required string Source { get; init; }

    public required SyncStatus Status { get; init; }

    /// <summary>Distinguishes accounts when a user has several credentials for one
    /// institution (Fio: one token per account), e.g. a masked account number.</summary>
    public string? AccountLabel { get; init; }

    /// <summary>Moment the institution reported the holdings as valid.</summary>
    public DateTimeOffset? AsOf { get; init; }

    public IReadOnlyList<SyncedHolding> Holdings { get; init; } = [];

    /// <summary>"Skip loudly": anything excluded or odd is reported here, never dropped
    /// silently — the number shown to the user must be honest about its gaps.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static ConnectorSyncResult Failed(string source, SyncStatus status) =>
        new() { Source = source, Status = status };
}
