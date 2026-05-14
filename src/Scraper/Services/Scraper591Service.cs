using System.Net.Http.Json;
using Scraper.Config;
using Scraper.Models;

namespace Scraper.Services;

public class Scraper591Service(HttpClient httpClient, System.Net.CookieContainer? cookieContainer = null)
{
    private static readonly Dictionary<string, string> DistrictCodes = new()
    {
        ["中正區"] = "1", ["大同區"] = "2", ["中山區"] = "3", ["松山區"] = "4",
        ["大安區"] = "7", ["萬華區"] = "5", ["信義區"] = "6", ["士林區"] = "8",
        ["北投區"] = "9", ["內湖區"] = "10", ["南港區"] = "11", ["文山區"] = "12"
    };

    // BFF kind codes: 整層住家=1, 獨立套房=2, 雅房=3, 分租套房=8
    private static readonly Dictionary<string, string> RoomTypeCodes = new()
    {
        ["整層住家"] = "1", ["獨立套房"] = "2", ["雅房"] = "3", ["分租套房"] = "8"
    };

    public async Task<List<SearchItem>> SearchListingsAsync(ScraperConfig config)
    {
        var sections = string.Join(",",
            config.Districts
                .Where(d => DistrictCodes.ContainsKey(d))
                .Select(d => DistrictCodes[d]));

        var types = string.Join(",",
            config.RoomTypes
                .Where(t => RoomTypeCodes.ContainsKey(t))
                .Select(t => RoomTypeCodes[t]));

        // Probe a few candidate BFF paths to find the correct one
        var candidates = new[]
        {
            $"https://bff.591.com.tw/v1/house/rent/search?type={types}&region=1&section={sections}&price={config.MaxPrice}&priceType=1&order=posttime&orderType=desc&firstRow=0&totalRows=30",
            $"https://bff.591.com.tw/v1/house/rent/list?type={types}&region=1&section={sections}&price={config.MaxPrice}&priceType=1&order=posttime&orderType=desc&firstRow=0&totalRows=30",
            $"https://bff.591.com.tw/v1/house/search?type={types}&region=1&section={sections}&price={config.MaxPrice}&order=posttime&orderType=desc&firstRow=0&totalRows=30",
        };

        foreach (var url in candidates)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "591/6.0.0 (iPhone; iOS 16.0)");
            req.Headers.Add("Accept", "application/json");

            var response = await httpClient.SendAsync(req);
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Debug] {url.Split('?')[0]} → {(int)response.StatusCode}: {body[..Math.Min(body.Length, 150)]}");

            if (response.IsSuccessStatusCode)
            {
                var result = await System.Text.Json.JsonSerializer.DeserializeAsync<SearchResponse>(
                    new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));
                if (result?.Status == "1")
                    return result.Data.Items
                        .Where(item => double.TryParse(item.Area, out var area) && area >= config.MinSizePing)
                        .ToList();
            }
        }

        return new List<SearchItem>();
    }

    public async Task<DetailData?> GetListingDetailAsync(string postId)
    {
        var url = $"https://bff.591.com.tw/v1/house/rent/detail?id={postId}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "591/6.0.0 (iPhone; iOS 16.0)");
        req.Headers.Add("Accept", "application/json");

        var response = await httpClient.SendAsync(req);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<DetailResponse>();
        return result?.Status == 1 ? result.Data : null;
    }
}
