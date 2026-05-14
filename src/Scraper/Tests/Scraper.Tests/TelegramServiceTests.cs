using Scraper.Models;
using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class TelegramServiceTests
{
    [Fact]
    public void FormatCaption_DoesNotIncludeEquipmentSection()
    {
        var service = new TelegramService(new HttpClient());
        var listing = new Listing
        {
            Title = "Test Listing",
            Price = 25000,
            Address = "台北市",
            SizePing = 10,
            RoomType = "套房",
            Url = "https://example.com"
        };

        var caption = service.FormatCaption(listing);

        Assert.DoesNotContain("家具", caption);
        Assert.DoesNotContain("天然氣", caption);
        Assert.DoesNotContain("第四台", caption);
        Assert.DoesNotContain("網路", caption);
        Assert.DoesNotContain("停車", caption);
        Assert.DoesNotContain("寵物", caption);
    }
}
