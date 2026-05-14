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
        RoomTypes = new List<string> { "套房" },
        RequireFurniture = false,
        RequireInternet = false
    };

    [Fact]
    public async Task SearchListings_ParsesItemsFromResponse()
    {
        var handler = new MockHttpMessageHandler();
        // More specific rules first — MockHttpMessageHandler matches in order
        handler.Setup("rsList", HttpStatusCode.OK, """
            {
              "status": "1",
              "data": {
                "records": "2",
                "data": [
                  {
                    "post_id": "111",
                    "title": "套房A",
                    "price": "15000",
                    "address": "台北市大安區",
                    "area": "10",
                    "kind_name": "套房",
                    "photo": "https://example.com/a.jpg"
                  }
                ]
              }
            }
            """);
        handler.Setup("rent.591.com.tw", HttpStatusCode.OK, ""); // catch-all for init request

        var svc = BuildService(handler);
        var items = await svc.SearchListingsAsync(BasicConfig());

        Assert.Single(items);
        Assert.Equal("111", items[0].PostId);
        Assert.Equal(15000, int.Parse(items[0].Price));
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
