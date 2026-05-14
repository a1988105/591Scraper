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
        handler.Setup("maps.googleapis.com", HttpStatusCode.OK, """
            {
              "status": "OK",
              "results": [{ "geometry": { "location": { "lat": 25.033, "lng": 121.565 } } }]
            }
            """);
        var svc = BuildService(handler);

        var result = await svc.GetCoordinatesAsync("台北市大安區復興南路一段", "dummy-key");

        Assert.NotNull(result);
        Assert.Equal(25.033, result.Value.Lat, precision: 3);
        Assert.Equal(121.565, result.Value.Lng, precision: 3);
    }

    [Fact]
    public async Task GetCoordinates_ZeroResults_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("maps.googleapis.com", HttpStatusCode.OK, """
            { "status": "ZERO_RESULTS", "results": [] }
            """);
        var svc = BuildService(handler);

        var result = await svc.GetCoordinatesAsync("不存在的地址xyz", "dummy-key");

        Assert.Null(result);
    }
}
