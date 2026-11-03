using PortfolioTrackerApp.Domain.Monetary;
using PortfolioTrackerApp.Domain.Monetary.Currency;

namespace PortfolioTrackerApp.Domain.Tests;

public partial class MoneyTests
{
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
        Assert.Equal(new Money(1717.20m, "EUR"), new Money(143.10m, "EUR") * 12m);
    }

    [Fact]
    public void Multiplication_works_with_the_number_on_the_left()
    {
        Assert.Equal(new Money(1717.20m, "EUR"), 12m * new Money(143.10m, "EUR"));
    }

    public static TheoryData<decimal, decimal> Negations => new()
    {
        { 100m, -100m },
        { -30m, 30m },
        { 0m, 0m },
    };

    [Theory]
    [MemberData(nameof(Negations))]
    public void Negation_flips_the_sign_and_keeps_currency(decimal amount, decimal expected)
    {
        Assert.Equal(new Money(expected, "CZK"), -new Money(amount, "CZK"));
    }

    [Fact]
    public void Decimal_math_is_exact_no_binary_float_drift()
    {
        Assert.Equal(new Money(0.3m, "EUR"), new Money(0.1m, "EUR") + new Money(0.2m, "EUR"));
    }

    // Mixing currencies is a bug or a missing explicit FX step, never something to guess through.
    [Fact]
    public void Adding_different_currencies_throws()
    {
        Assert.Throws<CurrencyMismatchException>(() => new Money(10m, "CZK") + new Money(10m, "EUR"));
        Assert.Throws<CurrencyMismatchException>(() => new Money(10m, "CZK") - new Money(10m, "EUR"));
    }

    // Rounding to minor units is a display concern, done at the edge — never in arithmetic.
    [Fact]
    public void Arithmetic_keeps_full_precision_no_rounding()
    {
        var third = new Money(10m, "CZK") * (1m / 3m);
        Assert.Equal(10m * (1m / 3m), third.Amount);
    }
}
