using PortfolioTrackerApp.Domain.Assets;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Domain.Holdings;

/// <summary>
/// A quantity of an asset held at one source at one moment — the normalized form
/// every connector translates into. Transient on the backend (per request); the
/// client vault is where positions persist. Value object: no identity, equal by contents.
/// Negative quantities are legal (short positions, liabilities).
/// </summary>
public sealed record Position
{
    public Asset Asset { get; }
    public decimal Quantity { get; }

    /// <summary>Which connector/source reported this holding, e.g. "fio", "ibkr", "manual".</summary>
    public string Source { get; }

    public DateTimeOffset AsOf { get; }

    public Position(Asset asset, decimal quantity, string source, DateTimeOffset asOf)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Position source is required.", nameof(source));

        Asset = asset;
        Quantity = quantity;
        Source = source.Trim();
        AsOf = asOf;
    }

    /// <summary>Cash holdings: quantity is the amount, unit price is 1 by construction.</summary>
    public static Position OfCash(Money amount, string source, DateTimeOffset asOf) =>
        new(Asset.Cash(amount.Currency), amount.Amount, source, asOf);

    /// <summary>
    /// The position's worth at a given unit price. The price must be quoted in the
    /// asset's quote currency — a mismatched price is a wiring bug, not an FX case.
    /// </summary>
    public Money ValueAt(Money unitPrice)
    {
        if (unitPrice.Currency != Asset.QuoteCurrency)
            throw new CurrencyMismatchException("value", Asset.QuoteCurrency, unitPrice.Currency);

        return unitPrice * Quantity;
    }
}
