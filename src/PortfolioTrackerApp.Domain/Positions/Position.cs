using PortfolioTrackerApp.Domain.Assets;
using PortfolioTrackerApp.Domain.Monetary;
using PortfolioTrackerApp.Domain.Monetary.Currency;

namespace PortfolioTrackerApp.Domain.Positions;

/// <summary>
/// Quantity of one asset at one source at one moment; every connector translates into it.
/// Value object. Invariants: source is required; negative quantities are legal (liabilities,
/// shorts); it is valued only at a unit price in the asset's quote currency, since a price in
/// another currency is a wiring bug, not an FX case. A cash position's quantity is its amount.
/// </summary>
public sealed record Position
{
    /// <summary>What is held, e.g. the VWCE asset or CZK cash.</summary>
    public Asset Asset { get; }

    /// <summary>Units held; for cash, the amount. Negative for liabilities and shorts, e.g. 12.5 (VWCE) or 195.01 (CZK cash).</summary>
    public decimal Quantity { get; }

    /// <summary>Identifier of the source that reported the position, e.g. "fio".</summary>
    public string Source { get; }

    /// <summary>Moment the source reported the quantity, e.g. 2026-11-03T10:15:00+00:00.</summary>
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

    public static Position OfCash(Money amount, string source, DateTimeOffset asOf) =>
        new(Asset.Cash(amount.Currency), amount.Amount, source, asOf);

    public Money ValueAt(Money unitPrice)
    {
        if (unitPrice.Currency != Asset.QuoteCurrency)
            throw new CurrencyMismatchException("value", Asset.QuoteCurrency, unitPrice.Currency);

        return unitPrice * Quantity;
    }
}
