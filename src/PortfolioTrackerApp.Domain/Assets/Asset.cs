using PortfolioTrackerApp.Domain.Holdings;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Domain.Assets;

public enum AssetType
{
    Cash = 1,
    Security = 2,
    Crypto = 3,
    Other = 4
}

/// <summary>
/// Identity of a thing that can be owned and priced — the instrument, not the holding.
/// "VWCE" is the same asset whether Fio, IBKR or a CSV mentions it; connectors resolve
/// their dialects onto this shared identity. Quantity lives on <see cref="Position"/>.
/// </summary>
public sealed record Asset
{
    public AssetType Type { get; }

    /// <summary>Primary external identifier: ticker, currency code, chain address…</summary>
    public string Symbol { get; }

    public string Name { get; }

    /// <summary>Currency the asset is quoted/valued in (unit prices must match it).</summary>
    public string QuoteCurrency { get; }

    public string? Isin { get; }

    public Asset(AssetType type, string symbol, string quoteCurrency, string? name = null, string? isin = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Asset symbol is required.", nameof(symbol));

        Type = type;
        Symbol = symbol.Trim();
        QuoteCurrency = CurrencyCode.Normalize(quoteCurrency, nameof(quoteCurrency));
        Name = string.IsNullOrWhiteSpace(name) ? Symbol : name.Trim();
        Isin = string.IsNullOrWhiteSpace(isin) ? null : isin.Trim();
    }

    /// <summary>Cash as an asset: symbol and quote currency are the currency itself.</summary>
    public static Asset Cash(string currency) => new(AssetType.Cash, currency, currency);
}
