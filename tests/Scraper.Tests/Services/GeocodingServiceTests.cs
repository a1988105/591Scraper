using System.Net;
using Scraper.Services;
using Scraper.Tests.Helpers;
using Xunit;

namespace Scraper.Tests.Services;

public class GeocodingServiceTests
{
    private static GeocodingService BuildService(MockHttpMessageHandler handler)
        => new(new HttpClient(handler));

    [Fact]
    public async Task GetCoordinates_ValidAddress_ReturnsLatLng()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("nominatim.openstreetmap.org", HttpStatusCode.OK, """
            [{"lat":"25.033","lon":"121.565","display_name":"台北市大安區復興南路一段"}]
            """);
        var svc = BuildService(handler);

        var result = await svc.GetCoordinatesAsync("台北市大安區復興南路一段");

        Assert.NotNull(result);
        Assert.Equal(25.033, result.Value.Lat, precision: 3);
        Assert.Equal(121.565, result.Value.Lng, precision: 3);
    }

    [Fact]
    public async Task GetCoordinates_ZeroResults_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("nominatim.openstreetmap.org", HttpStatusCode.OK, "[]");
        var svc = BuildService(handler);

        var result = await svc.GetCoordinatesAsync("不存在的地址xyz");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCoordinates_EmptyAddress_ReturnsNull()
    {
        var svc = BuildService(new MockHttpMessageHandler());

        var result = await svc.GetCoordinatesAsync("   ");

        Assert.Null(result);
    }

    // ── NormalizeAddress ─────────────────────────────────────────────

    [Theory]
    [InlineData("台北市大安區復興南路一段100號3F",  "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號3f",  "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號3樓", "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號B1F", "台北市大安區復興南路一段100號")]
    public void NormalizeAddress_StripsFloorSuffix(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Theory]
    [InlineData("台北市大安區復興南路一段100號(3樓)",   "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號（3F）",   "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號(大安社區)", "台北市大安區復興南路一段100號")]
    public void NormalizeAddress_StripsParentheses(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Theory]
    [InlineData("台北市大安區復興南路一段100號/近捷運站",   "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號／近大安站",  "台北市大安區復興南路一段100號")]
    public void NormalizeAddress_StripsSlashAndAfter(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Theory]
    [InlineData("台北市大安區復興南路一段100號近忠孝復興站", "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號近大安捷運",  "台北市大安區復興南路一段100號")]
    public void NormalizeAddress_StripsLandmarkDescription(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Theory]
    [InlineData("大安區復興南路一段100號", "台北市大安區復興南路一段100號")]
    [InlineData("永和區中正路100號",       "新北市永和區中正路100號")]
    public void NormalizeAddress_AddsMissingCityPrefix(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Theory]
    [InlineData("台北市大安區復興南路一段100號精緻套房", "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號優質雅房", "台北市大安區復興南路一段100號")]
    [InlineData("台北市大安區復興南路一段100號整層住家", "台北市大安區復興南路一段100號")]
    public void NormalizeAddress_StripsTrailingDescriptiveWords(string input, string expected)
        => Assert.Equal(expected, GeocodingService.NormalizeAddress(input));

    [Fact]
    public void NormalizeAddress_CleanAddress_IsUnchanged()
        => Assert.Equal("台北市大安區復興南路一段100號",
            GeocodingService.NormalizeAddress("台北市大安區復興南路一段100號"));

    // ── ExtractFallbackLevels ────────────────────────────────────────

    [Fact]
    public void ExtractFallbackLevels_FullAddress_ReturnsRoadLevel()
    {
        var levels = GeocodingService.ExtractFallbackLevels(
            "台北市大安區復興南路一段100號").ToList();

        Assert.Contains("台北市大安區復興南路一段", levels);
    }

    [Fact]
    public void ExtractFallbackLevels_WithSection_KeepsSectionInRoadLevel()
    {
        var levels = GeocodingService.ExtractFallbackLevels(
            "台北市永和區中正路二段50號").ToList();

        Assert.Contains("台北市永和區中正路二段", levels);
    }

    [Fact]
    public void ExtractFallbackLevels_AlreadyRoadLevel_ReturnsEmpty()
    {
        var levels = GeocodingService.ExtractFallbackLevels(
            "台北市大安區復興南路一段").ToList();

        Assert.Empty(levels);
    }

    [Fact]
    public void ExtractFallbackLevels_UnparsableAddress_ReturnsEmpty()
    {
        var levels = GeocodingService.ExtractFallbackLevels("不明地址xyz123").ToList();

        Assert.Empty(levels);
    }

    [Fact]
    public void ExtractFallbackLevels_Level2DiffersFromInput_IncludesLevel2()
    {
        // If NormalizeAddress couldn't clean everything, Level 2 is the structured rebuild
        var levels = GeocodingService.ExtractFallbackLevels(
            "台北市大安區復興南路一段100號精緻套房").ToList();

        // Level 2: parsed rebuild without trailing noise
        Assert.Contains("台北市大安區復興南路一段100號", levels);
        // Level 3: road-level
        Assert.Contains("台北市大安區復興南路一段", levels);
    }

    [Fact]
    public void ExtractFallbackLevels_LaneAddress_FallsBackToRoadSectionLevel()
    {
        // Known limitation: the regex does not parse 巷/弄 components.
        // 96巷10號 is not captured — the fallback degrades to road+section level.
        var levels = GeocodingService.ExtractFallbackLevels(
            "台北市大安區和平東路一段96巷10號").ToList();

        Assert.Contains("台北市大安區和平東路一段", levels);
    }
}
