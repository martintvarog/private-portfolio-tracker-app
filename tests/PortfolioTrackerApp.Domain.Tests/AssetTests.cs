using PortfolioTrackerApp.Domain.Assets;

namespace PortfolioTrackerApp.Domain.Tests;

public class AssetTests
{
    [Fact]
    public void Minimal_asset_has_name_defaulted_to_symbol_and_no_isin()
    {
        var asset = new Asset(AssetType.Security, "VWCE", "EUR");

        Assert.Equal(AssetType.Security, asset.Type);
        Assert.Equal("VWCE", asset.Symbol);
        Assert.Equal("EUR", asset.QuoteCurrency);
        Assert.Equal("VWCE", asset.Name);
        Assert.Null(asset.Isin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EURO")]
    public void Constructing_with_invalid_quote_currency_throws(string quoteCurrency)
    {
        Assert.Throws<ArgumentException>(() => new Asset(AssetType.Security, "VWCE", quoteCurrency));
    }

    [Fact]
    public void Constructing_with_empty_symbol_throws()
    {
        Assert.Throws<ArgumentException>(() => new Asset(AssetType.Security, "", "EUR"));
    }

    [Fact]
    public void Cash_asset_is_its_own_currency()
    {
        var cash = Asset.Cash("CZK");

        Assert.Equal(AssetType.Cash, cash.Type);
        Assert.Equal("CZK", cash.Symbol);
        Assert.Equal("CZK", cash.QuoteCurrency);
    }
}
