using Scraper.Models;

namespace Scraper.Services;

public class TelegramService(HttpClient httpClient)
{
    public async Task SendNotificationAsync(Listing listing, string botToken, string chatId)
    {
        var caption = FormatCaption(listing);
        var photoUrl = listing.Images.Count > 0 ? listing.Images[0] : null;

        string apiUrl;
        HttpContent content;

        if (photoUrl != null)
        {
            apiUrl = $"https://api.telegram.org/bot{botToken}/sendPhoto";
            content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["photo"] = photoUrl,
                ["caption"] = caption,
                ["parse_mode"] = "Markdown"
            });
        }
        else
        {
            apiUrl = $"https://api.telegram.org/bot{botToken}/sendMessage";
            content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = caption,
                ["parse_mode"] = "Markdown"
            });
        }

        var response = await httpClient.PostAsync(apiUrl, content);
        response.EnsureSuccessStatusCode();
    }

    public string FormatCaption(Listing listing)
    {
        return $"""
*🏠 {listing.Title}*

💰 *${listing.Price:N0} / 月*
📍 {listing.Address}
📐 {listing.SizePing} 坪｜{listing.RoomType}

[🔗 查看物件]({listing.Url})
""";
    }
}
