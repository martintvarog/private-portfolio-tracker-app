using PortfolioTrackerApp.Domain;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Domain.Tests;

// Executable spec for Money. The build stays RED until you create Money in Domain —
// that's on purpose (TDD). The API assumed here (ctor + Amount/Currency + operators)
// is a starting point, not a mandate: if you reshape it, reshape the tests with it.
// The invariants themselves are the contract.
public class MoneyTests
{
    // --- Value semantics: money is a value object, equal by contents ---

    [Fact]
    public void Two_moneys_with_same_amount_and_currency_are_equal()
    {
        Assert.Equal(new Money(100m, "CZK"), new Money(100m, "CZK"));
    }

    [Fact]
    public void Same_amount_different_currency_are_not_equal()
    {
        Assert.NotEqual(new Money(100m, "CZK"), new Money(100m, "EUR"));
    }

    // --- Arithmetic within one currency ---

    [Fact]
    public void Addition_sums_amounts_and_keeps_currency()
    {
        Assert.Equal(new Money(150m, "CZK"), new Money(100m, "CZK") + new Money(50m, "CZK"));
    }

    [Fact]
    public void Subtraction_can_go_negative_liabilities_exist()
    {
        Assert.Equal(new Money(-30m, "CZK"), new Money(20m, "CZK") - new Money(50m, "CZK"));
    }

    [Fact]
    public void Multiplication_by_quantity_scales_amount()
    {
        // quantity x unit price — the Position.value use case
        Assert.Equal(new Money(1717.20m, "EUR"), new Money(143.10m, "EUR") * 12m);
    }

    [Fact]
    public void Decimal_math_is_exact_no_binary_float_drift()
    {
        // 0.1 + 0.2 == 0.3 must hold exactly; with double it wouldn't
        Assert.Equal(new Money(0.3m, "EUR"), new Money(0.1m, "EUR") + new Money(0.2m, "EUR"));
    }

    // --- The three design decisions, pinned (see conversation/ADR — veto by changing test + code) ---

    // Decision 1: cross-currency arithmetic throws. Mixing currencies is always a bug
    // or a missing explicit FX step — silent coercion is how trackers lie.
    [Fact]
    public void Adding_different_currencies_throws()
    {
        Assert.Throws<CurrencyMismatchException>(() => new Money(10m, "CZK") + new Money(10m, "EUR"));
        Assert.Throws<CurrencyMismatchException>(() => new Money(10m, "CZK") - new Money(10m, "EUR"));
    }

    // Decision 2: constructor validates currency shape (3 ASCII letters) and normalizes case.
    // Shape-only check: unknown codes are fine, garbage is not.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CZKX")]
    [InlineData("C1K")]
    public void Constructing_with_invalid_currency_throws(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, currency));
    }

    [Fact]
    public void Currency_code_is_normalized_to_uppercase()
    {
        Assert.Equal(new Money(1m, "CZK"), new Money(1m, "czk"));
    }

    // Decision 3: Money never rounds. Full decimal precision through all arithmetic;
    // rounding to minor units is a display/allocation concern, done at the edge.
    [Fact]
    public void Arithmetic_keeps_full_precision_no_rounding()
    {
        var third = new Money(10m, "CZK") * (1m / 3m);
        Assert.Equal(10m * (1m / 3m), third.Amount); // untouched, not rounded to 3.33
        Assert.Equal(new Money(0.050m, "CZK"), new Money(0.10m, "CZK") * 0.5m);
    }
}
