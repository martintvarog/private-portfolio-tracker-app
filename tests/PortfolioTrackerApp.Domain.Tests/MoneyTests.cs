using System.Globalization;
using PortfolioTrackerApp.Domain.Monetary;

namespace PortfolioTrackerApp.Domain.Tests;

public partial class MoneyTests
{
    public static TheoryData<decimal, string> ValidMoney => new()
    {
        { 100m, "CZK" },
        { -50.25m, "EUR" },
        { 0m, "USD" },
    };

    [Theory]
    [MemberData(nameof(ValidMoney))]
    public void Valid_money_constructs_with_given_amount_and_currency(decimal amount, string currency)
    {
        var money = new Money(amount, currency);

        Assert.Equal(amount, money.Amount);
        Assert.Equal(currency, money.Currency);
    }

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

    // Shape-only check (3 ASCII letters): unknown codes are fine, garbage is not.
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

    public static TheoryData<decimal, string, string> Formatted => new()
    {
        { 1.5m, "CZK", "1.5 CZK" },
        { -1234.56m, "EUR", "-1234.56 EUR" },
    };

    // Same text on every machine: a Czech culture must not turn "1.5" into "1,5".
    [Theory]
    [MemberData(nameof(Formatted))]
    public void ToString_is_culture_independent(decimal amount, string currency, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
            Assert.Equal(expected, new Money(amount, currency).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
