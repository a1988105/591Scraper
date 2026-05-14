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

    private static readonly Dictionary<string, string> RoomTypeCodes = new()
    {
        ["整層住家"] = "1", ["獨立套房"] = "2", ["雅房"] = "3", ["分租套房"] = "8"
    };

    public async Task<List<SearchItem>> SearchListingsAsync(ScraperConfig config)
    {
        var initRequest = new HttpRequestMessage(HttpMethod.Get, "https://rent.591.com.tw/");
        initRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        initRequest.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        initRequest.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en;q=0.8");
        var initResp = await httpClient.SendAsync(initRequest);

        Console.WriteLine($"[Debug] init status={(int)initResp.StatusCode}");

        // Print all cookies stored by CookieContainer
        if (cookieContainer != null)
        {
            var allCookies = cookieContainer.GetCookies(new Uri("https://rent.591.com.tw/"));
            Console.WriteLine($"[Debug] all cookies: {string.Join(", ", allCookies.Cast<System.Net.Cookie>().Select(c => $"{c.Name}={c.Value[..Math.Min(c.Value.Length,10)]}..."))}");
        }

        // CookieContainer intercepts Set-Cookie headers — read T591_TOKEN directly from it
        var csrfToken = cookieContainer?
            .GetCookies(new Uri("https://rent.591.com.tw/"))["T591_TOKEN"]?.Value ?? "";

        // Fallback: try response headers (for unit tests using MockHttpMessageHandler)
        if (string.IsNullOrEmpty(csrfToken) && initResp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            csrfToken = setCookies
                .Select(c => c.Split(';')[0].Trim())
                .Where(c => c.StartsWith("T591_TOKEN="))
                .Select(c => c["T591_TOKEN=".Length..])
                .FirstOrDefault() ?? "";
        }

        Console.WriteLine($"[Debug] csrfToken={csrfToken}");

        var sections = string.Join(",",
            config.Districts
                .Where(d => DistrictCodes.ContainsKey(d))
                .Select(d => DistrictCodes[d]));

        var types = string.Join(",",
            config.RoomTypes
                .Where(t => RoomTypeCodes.ContainsKey(t))
                .Select(t => RoomTypeCodes[t]));

        var url = $"https://rent.591.com.tw/home/search/rsList" +
                  $"?is_new_list=1&type={types}&region=1&section={sections}" +
                  $"&price=0_{config.MaxPrice}&order=posttime&orderType=desc" +
                  $"&firstRow=0&totalRows=30";

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        req.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        req.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en;q=0.8");
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (!string.IsNullOrEmpty(csrfToken))
            req.Headers.Add("X-CSRF-Token", csrfToken);
        req.Headers.Add("Referer", "https://rent.591.com.tw/");

        var response = await httpClient.SendAsync(req);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Debug] search status={(int)response.StatusCode}, body={body[..Math.Min(body.Length, 300)]}");
        }
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        if (result?.Status != "1") return new List<SearchItem>();

        return result.Data.Items
            .Where(item => double.TryParse(item.Area, out var area) && area >= config.MinSizePing)
            .ToList();
    }

    public async Task<DetailData?> GetListingDetailAsync(string postId)
    {
        var url = $"https://bff.591.com.tw/v1/house/rent/detail?id={postId}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var response = await httpClient.SendAsync(req);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<DetailResponse>();
        return result?.Status == 1 ? result.Data : null;
    }
}
