namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// Outcome of one sync, sent as a string (ADR-0010). Anything but <see cref="Ok"/> means no positions:
/// <see cref="InvalidCredential"/> — the user must fix or renew the credential;
/// <see cref="Unavailable"/> and <see cref="RateLimited"/> — retry later.
/// No member is 0, so an unset value is detectable.
/// </summary>
public enum SyncStatus
{
    Ok = 1,
    InvalidCredential = 2,
    Unavailable = 3,
    RateLimited = 4,
}
