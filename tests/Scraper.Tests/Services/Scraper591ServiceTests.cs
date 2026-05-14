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

    private const string SampleListHtml = """
        <html><body>
        <div class="item" data-id="111">
          <div class="item-info-title" data-v-abc>套房A</div>
          <div class="price font-arial" data-v-abc>15,000</div>
          <div class="item-info-txt" data-v-abc>獨立套房10坪</div>
          <div class="item-info-txt" data-v-abc>台北市大安區某街道</div>
        </div>
        <div class="item" data-id="222">
          <div class="item-info-title" data-v-abc>小套房B</div>
          <div class="price font-arial" data-v-abc>5,000</div>
          <div class="item-info-txt" data-v-abc>獨立套房5坪</div>
          <div class="item-info-txt" data-v-abc>台北市大安區另一街道</div>
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
        Assert.Equal("15000", items[0].Price);
        Assert.Equal("獨立套房", items[0].KindName);
        Assert.Equal("10", items[0].Area);
        Assert.Equal("台北市大安區某街道", items[0].Address);
    }

    [Fact]
    public void ParseHtmlListings_FiltersSmallRooms()
    {
        var config = BasicConfig() with { MinSizePing = 6 };
        var items = Scraper591Service.ParseHtmlListings(SampleListHtml, config);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void ParseHtmlListings_ParsesPriceWithCommas()
    {
        var config = BasicConfig();
        var items = Scraper591Service.ParseHtmlListings(SampleListHtml, config);

        Assert.Equal("15000", items[0].Price);
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

    [Fact]
    public async Task GetListingDetail_ParsesAmenities()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("bff.591.com.tw", HttpStatusCode.OK, """
            {
              "status": 1,
              "data": {
                "id": "111",
                "photo_list": [{"src": "https://example.com/photo.jpg"}],
                "furniture": 1,
                "natural_gas": 0,
                "cable_tv": 0,
                "broadband": 1,
                "parking_space": 0,
                "can_keep_pet": 0
              }
            }
            """);

        var svc = BuildService(handler);
        var detail = await svc.GetListingDetailAsync("111");

        Assert.NotNull(detail);
        Assert.Single(detail!.PhotoList);
        Assert.Equal(1, detail.Furniture);
        Assert.Equal(1, detail.Broadband);
    }
}
