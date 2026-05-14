using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class GeocodingServiceTests
{
    [Fact]
    public void TryBuildGeocodingUri_ReturnsFalseForOverlongAddress()
    {
        var longAddress = new string('A', 5000);

        var ok = GeocodingService.TryBuildGeocodingUri(longAddress, "test-key", out var uri);

        Assert.False(ok);
        Assert.Null(uri);
    }

    [Fact]
    public void TryBuildGeocodingUri_ReturnsEncodedUriForNormalAddress()
    {
        var ok = GeocodingService.TryBuildGeocodingUri("台北市 大安區", "test-key", out var uri);

        Assert.True(ok);
        Assert.NotNull(uri);
        Assert.Contains("address=", uri!.Query);
        Assert.Contains("key=test-key", uri.Query);
        Assert.Contains("language=zh-TW", uri.Query);
    }
}
