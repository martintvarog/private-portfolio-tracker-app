using PortfolioTrackerApp.Domain.Monetary.Currency;

namespace PortfolioTrackerApp.Domain.Assets;

/// <summary>Kind of asset. No member is 0, so an unset value is detectable.</summary>
public enum AssetType
{
    Cash = 1,
    Security = 2,
    Crypto = 3,
    Other = 4
}

/// <summary>
/// A thing that can be owned and priced: the instrument, not the holding of it ("VWCE" is the
/// same asset at every source). Value object. Invariants: symbol is required; quote currency is
/// a valid currency code and every unit price for this asset must be in it; name defaults to the
/// symbol. Cash is the asset whose symbol and quote currency are its own currency.
/// </summary>
public sealed record Asset
{
    /// <summary>Kind of asset: cash, security, crypto or other, e.g. Security for VWCE.</summary>
    public AssetType Type { get; }

    /// <summary>Identifier the asset is known by: ticker, currency code, coin symbol, e.g. "VWCE", "CZK", "BTC".</summary>
    public string Symbol { get; }

    /// <summary>Display name; the symbol when none is given, e.g. "Vanguard FTSE All-World UCITS ETF".</summary>
    public string Name { get; }

    /// <summary>Currency the asset is priced in; every unit price for it must be in this currency, e.g. "EUR" for VWCE.</summary>
    public string QuoteCurrency { get; }

    /// <summary>International Securities Identification Number, if the asset has one, e.g. "IE00BK5BQT80".</summary>
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

    public static Asset Cash(string currency) => new(AssetType.Cash, currency, currency);
}
