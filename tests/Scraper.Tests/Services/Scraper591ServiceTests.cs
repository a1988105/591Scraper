using System.Net;
using Scraper.Config;
using Scraper.Services;
using Scraper.Tests.Helpers;
using Xunit;

namespace Scraper.Tests.Services;

public class Scraper591ServiceTests
{
    private static Scraper591Service BuildService(MockHttpMessageHandler handler)
        => new(new HttpClient(handler));

    private static ScraperConfig BasicConfig() => new()
    {
        MaxPrice = 20000,
        MinSizePing = 8,
        Districts = new List<string> { "大安區" },
        RoomTypes = new List<string> { "獨立套房" },
        RequireFurniture = false,
        RequireInternet = false
    };

    private static ScraperConfig ConfigWithMinSizePing(double minSizePing)
    {
        var config = BasicConfig();
        config.MinSizePing = minSizePing;
        return config;
    }

    private const string SampleListHtml = """
        <html><body>
        <div class="item" data-id="111">
          <div class="item-info-title">
            <a href="https://rent.591.com.tw/111" target="_blank" title="套房A">套房A</a>
          </div>
          <div class="price-info" data-v-93fbb22b="">
          <span class="price font-arial" data-v-93fbb22b="">16,888</span><span class="unit" data-v-93fbb22b="">元/月</span></div>
          <div class="item-info-txt">
            <i class="ic-house house-home"></i><span>獨立套房</span>
            <span class="line"><div class="inline-flex-row">10坪</div></span>
          </div>
          <div class="item-info-txt">
            <i class="ic-house house-place"></i>
            <span><div class="inline-flex-row">台北市-大安區某街道</div></span>
          </div>
        </div>
        <div class="item" data-id="222">
          <div class="item-info-title">
            <a href="https://rent.591.com.tw/222" target="_blank" title="小套房B">小套房B</a>
          </div>
          <div class="price-info">
            <div class="price font-arial">5,000</div>
          </div>
          <div class="item-info-txt">
            <i class="ic-house house-home"></i><span>獨立套房</span>
            <span class="line"><div class="inline-flex-row">5坪</div></span>
          </div>
          <div class="item-info-txt">
            <i class="ic-house house-place"></i>
            <span><div class="inline-flex-row">台北市-大安區另一街道</div></span>
          </div>
        </div>
        </body></html>
        """;

    [Fact]
    public async Task SearchListings_ParsesItemsFromHtml()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("rent.591.com.tw/list", HttpStatusCode.OK, SampleListHtml);

        var svc = BuildService(handler);
        var items = await svc.SearchListingsAsync(BasicConfig());

        Assert.Single(items); // 222 filtered out (5坪 < 8坪 MinSizePing)
        Assert.Equal("111", items[0].PostId);
        Assert.Equal("16888", items[0].Price);
        Assert.Equal("獨立套房", items[0].KindName);
        Assert.Equal("10", items[0].Area);
        Assert.Equal("台北市-大安區某街道", items[0].Address);
    }

    [Fact]
    public void ParseHtmlListings_FiltersSmallRooms()
    {
        var config = ConfigWithMinSizePing(5);
        var items = Scraper591Service.ParseHtmlListings(SampleListHtml, config);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void ParseHtmlListings_ParsesPriceWithCommas()
    {
        var config = BasicConfig();
        var items = Scraper591Service.ParseHtmlListings(SampleListHtml, config);

        Assert.Equal("16888", items[0].Price);
    }

    [Fact]
    public async Task SearchListings_ReturnsEmptyOnNonSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("rent.591.com.tw/list", HttpStatusCode.Forbidden, "");

        var svc = BuildService(handler);
        var items = await svc.SearchListingsAsync(BasicConfig());

        Assert.Empty(items);
    }

    private const string SampleDetailHtml = """
        <html><body>
        <div class="facility service-facility" data-v-a31d3795="" data-v-cfafc3c0=""><!--[-->
        <dl class="" data-v-cfafc3c0=""><dt data-v-cfafc3c0=""><i class="ic-house house-bed" data-v-cfafc3c0=""></i></dt><dd class="text" data-v-cfafc3c0="">床</dd></dl>
        <dl class="" data-v-cfafc3c0=""><dt data-v-cfafc3c0=""><i class="ic-house house-gas" data-v-cfafc3c0=""></i></dt><dd class="text" data-v-cfafc3c0="">天然瓦斯</dd></dl>
        <dl class="del" data-v-cfafc3c0=""><dt data-v-cfafc3c0=""><i class="ic-house house-fourth" data-v-cfafc3c0=""></i></dt><dd class="text" data-v-cfafc3c0="">第四台</dd></dl>
        <dl class="" data-v-cfafc3c0=""><dt data-v-cfafc3c0=""><i class="ic-house house-net" data-v-cfafc3c0=""></i></dt><dd class="text" data-v-cfafc3c0="">網路</dd></dl>
        <dl class="del" data-v-cfafc3c0=""><dt data-v-cfafc3c0=""><i class="ic-house house-parking" data-v-cfafc3c0=""></i></dt><dd class="text" data-v-cfafc3c0="">車位</dd></dl>
        <!--]--></div>
        </body></html>
        """;

    [Fact]
    public void ParseDetailHtml_ParsesFacilitiesFromHtml()
    {
        var detail = Scraper591Service.ParseDetailHtml(SampleDetailHtml, "111");

        Assert.NotNull(detail);
        Assert.Equal("111", detail!.Id);
        Assert.Equal(1, detail.Furniture);      // 床 available
        Assert.Equal(1, detail.NaturalGas);     // 天然瓦斯 available
        Assert.Equal(0, detail.CableTv);        // 第四台 del
        Assert.Equal(1, detail.Broadband);      // 網路 available
        Assert.Equal(0, detail.ParkingSpace);   // 車位 del
    }

    [Fact]
    public async Task GetListingDetail_FetchesHtmlDetailPage()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("rent.591.com.tw/rent-detail-", HttpStatusCode.OK, SampleDetailHtml);

        var svc = BuildService(handler);
        var detail = await svc.GetListingDetailAsync("111");

        Assert.NotNull(detail);
        Assert.Equal(1, detail!.NaturalGas);
        Assert.Equal(0, detail.CableTv);
    }
}
