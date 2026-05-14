using Scraper.Config;
using Scraper.Services;
using System.Reflection;
using Xunit;

namespace Scraper.Tests;

public class Scraper591ServiceTests
{
    [Fact]
    public void BuildSearchUrl_UsesConfiguredRegion()
    {
        var config = new ScraperConfig
        {
            Region = 3,
            MinPrice = 10000,
            MaxPrice = 20000
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.Contains("region=3", url);
        Assert.Contains("price=10000_20000", url);
    }

    [Fact]
    public void BuildSearchUrl_OmitsKindWhenNoRoomTypesConfigured()
    {
        var config = new ScraperConfig
        {
            Region = 1,
            MaxPrice = 20000,
            RoomTypes = []
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.DoesNotContain("kind=", url);
    }

    [Fact]
    public void BuildSearchUrl_IncludesKindWhenMappedRoomTypesExist()
    {
        var roomTypeCodesField = typeof(Scraper591Service).GetField(
            "RoomTypeCodes",
            BindingFlags.NonPublic | BindingFlags.Static);
        var roomTypeCodes = Assert.IsType<Dictionary<string, string>>(roomTypeCodesField?.GetValue(null));
        var mappedRoomType = roomTypeCodes.Keys.First();

        var config = new ScraperConfig
        {
            Region = 1,
            MaxPrice = 20000,
            RoomTypes = [mappedRoomType]
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.Contains("kind=1", url);
    }

    [Fact]
    public void BuildSearchUrl_UsesExplicitSectionCodes()
    {
        var config = new ScraperConfig
        {
            Region = 3,
            MaxPrice = 20000,
            SectionCodes = [26, 44]
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.Contains("section=26,44", url);
    }

    [Fact]
    public void BuildSearchUrl_IncludesFirstRowForLaterPages()
    {
        var config = new ScraperConfig
        {
            Region = 3,
            MinPrice = 10000,
            MaxPrice = 20000,
            SectionCodes = [37]
        };

        var url = Scraper591Service.BuildSearchUrl(config, page: 2);

        Assert.Contains("page=2", url);
        Assert.Contains("firstRow=30", url);
    }

    [Fact]
    public void BuildSearchUrl_ThrowsWhenConfiguredDistrictsCannotBeMapped()
    {
        var config = new ScraperConfig
        {
            Region = 3,
            MaxPrice = 20000,
            Districts = ["板橋區"]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Scraper591Service.BuildSearchUrl(config));

        Assert.Contains("Districts", ex.Message);
        Assert.Contains("板橋區", ex.Message);
    }

    [Fact]
    public void ParseHtmlListings_UsesAddressFieldInsteadOfLastHyphenatedText()
    {
        const string html = """
            <div class="item" data-id="123">
              <a title="測試物件"></a>
              <div class="price font-arial">20,000</div>
              <div>套房</div>
              <div class="line"><span>10</span></div>
              <div class="item-info-txt">套房 | 10坪</div>
              <div class="item-info-txt">台北市-大安區和平東路</div>
              <div class="meta">2026-05-14 更新，這不是地址</div>
            </div>
            """;

        var config = new ScraperConfig
        {
            MinSizePing = 1
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        var item = Assert.Single(items);
        Assert.Equal("台北市-大安區和平東路", item.Address);
    }

    [Fact]
    public void ParseHtmlListings_ParsesAddressWhenItemInfoTextContainsNestedTags()
    {
        const string html = """
            <div class="item" data-id="123">
              <a title="測試物件"></a>
              <div class="price font-arial">20,000</div>
              <div>套房</div>
              <div class="line"><span>10</span></div>
              <div class="item-info-txt"><span>套房</span> | <span>10坪</span></div>
              <div class="item-info-txt"><a>台北市-大安區和平東路</a></div>
            </div>
            """;

        var config = new ScraperConfig
        {
            MinSizePing = 1
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        var item = Assert.Single(items);
        Assert.Equal("台北市-大安區和平東路", item.Address);
    }

    [Fact]
    public void ParseHtmlListings_UsesSecondItemInfoTextAsAddressWhenExtraFieldsExist()
    {
        const string html = """
            <div class="item" data-id="123">
              <a title="測試物件"></a>
              <div class="price font-arial">20,000</div>
              <div>套房</div>
              <div class="line"><span>10</span></div>
              <div class="item-info-txt"><span>套房</span> | <span>10坪</span></div>
              <div class="item-info-txt"><a>台北市-大安區和平東路</a></div>
              <div class="item-info-txt">近捷運站</div>
            </div>
            """;

        var config = new ScraperConfig
        {
            MinSizePing = 1
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        var item = Assert.Single(items);
        Assert.Equal("台北市-大安區和平東路", item.Address);
    }
    [Fact]
    public void ParseHtmlListings_ParsesPriceWhenPriceNodeContainsNestedTags()
    {
        const string html = """
            <div class="item" data-id="123">
              <a title="測試物件"></a>
              <div class="price font-arial"><span>20,000</span></div>
              <div class="line"><span>10</span></div>
              <div class="item-info-txt">套房 | 10坪</div>
              <div class="item-info-txt">台北市-大安區和平東路</div>
            </div>
            """;

        var config = new ScraperConfig
        {
            MinSizePing = 1
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        var item = Assert.Single(items);
        Assert.Equal("20000", item.Price);
    }

    [Fact]
    public void ParseHtmlListings_ParsesPriceFromItemInfoPriceBlock()
    {
        const string html = """
            <div class="item" data-id="123">
              <div class="item-info">
                <div class="item-info-title">
                  <a title="永和套房"></a>
                </div>
                <div class="item-info-left">
                  <div class="item-info-txt"><span>套房</span><span class="line"><div class="inline-flex-row">6坪</div></span></div>
                  <div class="item-info-txt"><span><div class="inline-flex-row">新北市永和區中正路</div></span></div>
                </div>
                <div class="item-info-price">
                  <div class="color-#F01800">
                    <strong class="text-26px font-arial">
                      <div class="inline-flex-row">7,500</div>
                    </strong>
                    <span class="text-14px ml-2px">元/月</span>
                  </div>
                </div>
              </div>
            </div>
            """;

        var config = new ScraperConfig
        {
            MinSizePing = 1
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        var item = Assert.Single(items);
        Assert.Equal("7500", item.Price);
    }

    [Fact]
    public void ParseHtmlListings_ParsesNonZeroPricesFromLocal591Fixture()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var html = File.ReadAllText(Path.Combine(projectRoot, "591.html"));
        var config = new ScraperConfig
        {
            MinSizePing = 1,
            MaxPrice = 50000
        };

        var items = Scraper591Service.ParseHtmlListings(html, config);

        Assert.NotEmpty(items);
        Assert.Contains(items, item => item.Price != "0");
    }
}
