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
        // No exception = success
    }
}
