using Scraper.Config;
using Scraper.Models;
using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class EquipmentFilterTests
{
    [Fact]
    public void ShouldInclude_WhenInternetRequiredAndDetailUnavailable()
    {
        var config = new ScraperConfig
        {
            RequireInternet = true
        };

        var listing = new Listing
        {
            HasInternet = false,
            HasFurniture = false
        };

        var include = EquipmentFilter.ShouldInclude(config, listing, detailFetched: false);

        Assert.True(include);
    }

    [Fact]
    public void ShouldExclude_WhenInternetRequiredAndDetailFetchedWithoutInternet()
    {
        var config = new ScraperConfig
        {
            RequireInternet = true
        };

        var listing = new Listing
        {
            HasInternet = false,
            HasFurniture = false
        };

        var include = EquipmentFilter.ShouldInclude(config, listing, detailFetched: true);

        Assert.False(include);
    }

    [Fact]
    public void ShouldExclude_WhenFurnitureRequiredAndDetailFetchedWithoutFurniture()
    {
        var config = new ScraperConfig
        {
            RequireFurniture = true
        };

        var listing = new Listing
        {
            HasInternet = true,
            HasFurniture = false
        };

        var include = EquipmentFilter.ShouldInclude(config, listing, detailFetched: true);

        Assert.False(include);
    }
}
