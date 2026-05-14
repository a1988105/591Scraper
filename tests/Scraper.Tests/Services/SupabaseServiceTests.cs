using System.Net;
using Scraper.Models;
using Scraper.Services;
using Scraper.Tests.Helpers;
using Xunit;

namespace Scraper.Tests.Services;

public class SupabaseServiceTests
{
    private static SupabaseService BuildService(MockHttpMessageHandler handler)
        => new(new HttpClient(handler), "https://fake.supabase.co", "fake-key");

    [Fact]
    public async Task GetExistingIds_ReturnsSetOfIds()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.OK,
            """[{"id":"aaa"},{"id":"bbb"}]""");

        var svc = BuildService(handler);
        var ids = await svc.GetExistingIdsAsync(new[] { "aaa", "bbb", "ccc" });

        Assert.Contains("aaa", ids);
        Assert.Contains("bbb", ids);
        Assert.DoesNotContain("ccc", ids);
    }

    [Fact]
    public async Task UpsertListings_EmptyList_DoesNothing()
    {
        var handler = new MockHttpMessageHandler();
        var svc = BuildService(handler);

        // Should not throw
        await svc.UpsertListingsAsync(new List<Listing>());
    }

    [Fact]
    public async Task MarkNotified_CallsUpdateEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.OK, "[]");

        var svc = BuildService(handler);
        await svc.MarkNotifiedAsync(new[] { "id1", "id2" });
    }

    [Fact]
    public async Task GetListingsWithoutCoordinates_ReturnsIdAndAddress()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.OK,
            """[{"id":"abc","address":"台北市大安區復興南路一段"}]""");

        var svc = BuildService(handler);
        var result = await svc.GetListingsWithoutCoordinatesAsync();

        Assert.Single(result);
        Assert.Equal("abc", result[0].Id);
        Assert.Equal("台北市大安區復興南路一段", result[0].Address);
    }

    [Fact]
    public async Task UpdateCoordinates_PatchesLatLng()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.NoContent, "");

        var svc = BuildService(handler);
        await svc.UpdateCoordinatesAsync("abc", 25.033, 121.565);
        // No exception = success
    }
}
