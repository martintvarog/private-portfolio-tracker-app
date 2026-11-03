namespace PortfolioTrackerApp.Connectors.Contracts;

/// <summary>
/// One position in a <see cref="ConnectorSyncResult"/>, shaped for the wire (ADR-0003).
/// Cash: Symbol = Currency = the currency code, Quantity = the balance.
/// </summary>
public sealed record SyncedPosition
{
    /// <summary>Kind of asset: "cash" | "security" | "crypto" | "other", e.g. "cash" for a bank balance.</summary>
    public required string AssetType { get; init; }

    /// <summary>Identifier the asset is known by, e.g. "VWCE", "CZK", "BTC".</summary>
    public required string Symbol { get; init; }

    /// <summary>Units held; for cash, the amount. Negative for liabilities, e.g. 12.5 (VWCE) or 195.01 (CZK cash).</summary>
    public required decimal Quantity { get; init; }

    /// <summary>3-letter code of the currency the asset is priced in, e.g. "EUR" for VWCE, "CZK" for CZK cash.</summary>
    public required string Currency { get; init; }

    /// <summary>Display name, if the source provides one, e.g. "Vanguard FTSE All-World UCITS ETF".</summary>
    public string? Name { get; init; }

    /// <summary>International Securities Identification Number, if the source provides one, e.g. "IE00BK5BQT80".</summary>
    public string? Isin { get; init; }
}
