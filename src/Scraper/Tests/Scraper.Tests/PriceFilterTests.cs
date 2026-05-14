using Scraper.Config;
using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class PriceFilterTests
{
    [Fact]
    public void IsWithinRange_ReturnsTrue_WhenPriceWithinBounds()
    {
        var config = new ScraperConfig
        {
            MinPrice = 10000,
            MaxPrice = 30000
        };

        Assert.True(PriceFilter.IsWithinRange("25000", config));
    }

    [Fact]
    public void IsWithinRange_ReturnsFalse_WhenPriceBelowMin()
    {
        var config = new ScraperConfig
        {
            MinPrice = 10000,
            MaxPrice = 30000
        };

        Assert.False(PriceFilter.IsWithinRange("9000", config));
    }

    [Fact]
    public void IsWithinRange_ReturnsFalse_WhenPriceAboveMax()
    {
        var config = new ScraperConfig
        {
            MinPrice = 10000,
            MaxPrice = 30000
        };

        Assert.False(PriceFilter.IsWithinRange("31000", config));
    }

    [Fact]
    public void IsWithinRange_ReturnsTrue_WhenPriceCannotBeParsed()
    {
        var config = new ScraperConfig
        {
            MinPrice = 10000,
            MaxPrice = 30000
        };

        Assert.True(PriceFilter.IsWithinRange("0", config));
        Assert.True(PriceFilter.IsWithinRange("", config));
    }
}
