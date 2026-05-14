using System.Net;
using Scraper.Models;
using Scraper.Services;
using Scraper.Tests.Helpers;
using Xunit;

namespace Scraper.Tests.Services;

public class TelegramServiceTests
{
    private static TelegramService BuildService(MockHttpMessageHandler handler)
        => new(new HttpClient(handler));

    private static Listing SampleListing() => new()
    {
        Id = "12345",
        Title = "大安區精緻套房",
        Price = 15500,
        Address = "台北市大安區復興南路一段320號",
        SizePing = 10,
        RoomType = "套房",
        HasFurniture = true,
        HasNaturalGas = true,
        HasCableTv = false,
        HasInternet = true,
        HasParking = false,
        PetAllowed = false,
        Url = "https://rent.591.com.tw/rent-detail-12345.html",
        Images = new List<string> { "https://example.com/photo.jpg" }
    };

    [Fact]
    public async Task SendNotification_WithPhoto_CallsSendPhotoEndpoint()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("sendPhoto", HttpStatusCode.OK, """{"ok":true}""");

        var svc = BuildService(handler);
        await svc.SendNotificationAsync(SampleListing(), "fake-token", "fake-chat-id");

        // If no exception is thrown, sendPhoto was called successfully
    }

    [Fact]
    public async Task FormatCaption_ContainsKeyInfo()
    {
        var svc = new TelegramService(new HttpClient(new MockHttpMessageHandler()));
        var caption = svc.FormatCaption(SampleListing());

        Assert.Contains("大安區精緻套房", caption);
        Assert.Contains("15,500", caption);
        Assert.Contains("台北市大安區", caption);
        Assert.Contains("✅", caption); // has_furniture
    }
}
