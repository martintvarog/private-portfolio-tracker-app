namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// Per-connector outcome, returned as data (HTTP status stays reserved for transport
/// problems of our own API). No member at 0 so an uninitialized value is detectable.
/// </summary>
public enum SyncStatus
{
    /// <summary>Holdings fetched successfully.</summary>
    Ok = 1,

    /// <summary>Credential rejected by the institution — non-existent, mistyped,
    /// deactivated or expired; the user must fix or renew it (e.g. Fio signals
    /// an invalid/inactive token with HTTP 500).</summary>
    InvalidCredential = 2,

    /// <summary>The institution is unreachable or failing; retry later.</summary>
    Unavailable = 3,

    /// <summary>The institution throttled us (e.g. Fio: 1 request per token per 30 s);
    /// retry after the window.</summary>
    RateLimited = 4,
}
