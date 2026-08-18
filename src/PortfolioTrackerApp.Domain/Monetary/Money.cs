using System.Globalization;

namespace PortfolioTrackerApp.Domain.Monetary;

/// <summary>
/// An exact amount bound to a currency. Value object: immutable, equal by contents.
/// Invariants: amount is decimal (never binary floating point); currency is a
/// normalized ISO-4217-shaped code; arithmetic never crosses currencies and never
/// rounds — rounding is a presentation/allocation concern, not an arithmetic one.
/// Negative amounts are legal (liabilities exist).
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
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
