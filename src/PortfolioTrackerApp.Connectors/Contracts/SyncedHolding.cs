namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// One holding as reported by a connector — contract-owned (ADR-0003), so shaped for
/// the wire, not for the domain. Cash is the degenerate case: Kind "cash",
/// Symbol = currency code, Quantity = the balance.
/// </summary>
public sealed record SyncedHolding
{
    /// <summary>Wire discriminator: "cash" | "security" | "crypto" | "other".</summary>
    public required string Kind { get; init; }

    /// <summary>"CZK", "VWCE", "BTC" — the instrument identifier, never account-specific.</summary>
    public required string Symbol { get; init; }

    /// <summary>Units held; for cash, the amount. Amount and currency stay together.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Currency the holding is priced in; for cash, the currency itself.</summary>
    public required string Currency { get; init; }

    public string? Name { get; init; }

    public string? Isin { get; init; }
}
