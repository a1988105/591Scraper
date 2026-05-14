using System.Net;
using WebApp.Models;
using WebApp.Services;
using WebApp.Tests.Helpers;
using Xunit;

namespace WebApp.Tests.Services;

public class SupabaseServiceTests
{
    private static SupabaseService Build(MockHttpMessageHandler handler)
        => new(new HttpClient(handler), "https://fake.supabase.co", "fake-key");

    [Fact]
    public async Task GetListings_ReturnsDeserializedListings()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.OK, """
            [
              {"id":"1","title":"套房A","price":15000,"address":"大安區","lat":25.033,
               "lng":121.565,"size_ping":10,"room_type":"套房","has_furniture":true,
               "has_natural_gas":false,"has_cable_tv":false,"has_internet":true,
               "has_parking":false,"pet_allowed":false,"url":"https://x.com","images":[],
               "scraped_at":"2026-05-14T00:00:00Z"}
            ]
            """);

        var svc = Build(handler);
        var listings = await svc.GetListingsAsync();

        Assert.Single(listings);
        Assert.Equal("套房A", listings[0].Title);
        Assert.True(listings[0].HasInternet);
    }

    [Fact]
    public async Task GetFavoritesWithListings_JoinsListingData()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.OK, """
            [
              {"listing_id":"1","note":"","status":"待看","favorited_at":"2026-05-14T00:00:00Z",
               "listing":{"id":"1","title":"套房A","price":15000,"address":"大安區",
                "lat":25.033,"lng":121.565,"size_ping":10,"room_type":"套房",
                "has_furniture":true,"has_natural_gas":false,"has_cable_tv":false,
                "has_internet":true,"has_parking":false,"pet_allowed":false,
                "url":"https://x.com","images":[],"scraped_at":"2026-05-14T00:00:00Z"}}
            ]
            """);

        var svc = Build(handler);
        var favs = await svc.GetFavoritesWithListingsAsync();

        Assert.Single(favs);
        Assert.Equal("套房A", favs[0].Listing?.Title);
        Assert.Equal("待看", favs[0].Status);
    }

    [Fact]
    public async Task AddFavorite_PostsToSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.Created, "");

        var svc = Build(handler);
        await svc.AddFavoriteAsync("listing-id-1");
    }

    [Fact]
    public async Task RemoveFavorite_DeletesFromSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.NoContent, "");

        var svc = Build(handler);
        await svc.RemoveFavoriteAsync("listing-id-1");
    }

    [Fact]
    public async Task UpdateFavorite_PatchesSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.NoContent, "");

        var svc = Build(handler);
        await svc.UpdateFavoriteAsync("listing-id-1", new FavoriteUpdateRequest
        {
            Status = "已看",
            Note = "下週去看"
        });
    }
}
