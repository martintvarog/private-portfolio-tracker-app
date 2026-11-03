using PortfolioTrackerApp.Domain.Assets;
using PortfolioTrackerApp.Domain.Positions;
using PortfolioTrackerApp.Domain.Monetary;
using PortfolioTrackerApp.Domain.Monetary.Currency;

namespace PortfolioTrackerApp.Domain.Tests;

public class PositionTests
{
    private static readonly Asset Vwce =
        new(AssetType.Security, "VWCE", "EUR", "Vanguard FTSE All-World", isin: "IE00BK5BQT80");

    private static readonly DateTimeOffset AsOf = new(2026, 8, 10, 14, 32, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Position_constructs_with_given_asset_quantity_source_and_time()
    {
        var position = new Position(Vwce, 12m, "ibkr", AsOf);

        Assert.Equal(Vwce, position.Asset);
        Assert.Equal(12m, position.Quantity);
        Assert.Equal("ibkr", position.Source);
        Assert.Equal(AsOf, position.AsOf);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Position_requires_a_source(string source)
    {
        Assert.Throws<ArgumentException>(() => new Position(Vwce, 1m, source, AsOf));
    }

    [Fact]
    public void Cash_position_holds_the_amount_as_quantity_of_its_currency()
    {
        var cash = Position.OfCash(new Money(84_000m, "CZK"), "fio", AsOf);

        Assert.Equal(Asset.Cash("CZK"), cash.Asset);
        Assert.Equal(84_000m, cash.Quantity);
        Assert.Equal("fio", cash.Source);
        Assert.Equal(AsOf, cash.AsOf);
    }

    [Fact]
    public void Value_is_quantity_times_unit_price()
    {
        var position = new Position(Vwce, 12m, "ibkr", AsOf);

        Assert.Equal(new Money(1717.20m, "EUR"), position.ValueAt(new Money(143.10m, "EUR")));
    }

    [Fact]
    public void Valuing_with_price_in_wrong_currency_throws()
    {
        var position = new Position(Vwce, 12m, "ibkr", AsOf);

        // VWCE is quoted in EUR; a CZK price is a wiring bug, not an FX conversion.
        Assert.Throws<CurrencyMismatchException>(() => position.ValueAt(new Money(3520m, "CZK")));
    }

    [Fact]
    public void Negative_quantity_is_legal_liabilities_and_shorts_exist()
    {
        var position = new Position(Vwce, -3m, "ibkr", AsOf);

        Assert.Equal(new Money(-429.30m, "EUR"), position.ValueAt(new Money(143.10m, "EUR")));
    }
}
