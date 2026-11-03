using System.Globalization;
using PortfolioTrackerApp.Domain.Monetary.Currency;

namespace PortfolioTrackerApp.Domain.Monetary;

/// <summary>
/// An exact amount in one currency. Value object. Invariants: currency is a valid, upper-cased
/// currency code; arithmetic never mixes currencies and never rounds (rounding belongs to
/// display); negative amounts are legal (liabilities).
/// </summary>
public sealed record Money
{
    /// <summary>Exact amount; negative for liabilities, e.g. 1250.50 or -300.</summary>
    public decimal Amount { get; }

    /// <summary>Upper-cased 3-letter currency code, e.g. "CZK".</summary>
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = CurrencyCode.Normalize(currency, nameof(currency));
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "add");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "subtract");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money money) => new(-money.Amount, money.Currency);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.Currency);

    public static Money operator *(decimal factor, Money money) => money * factor;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");

    private static void EnsureSameCurrency(Money left, Money right, string operation)
    {
        if (left.Currency != right.Currency)
            throw new CurrencyMismatchException(operation, left.Currency, right.Currency);
    }
}
