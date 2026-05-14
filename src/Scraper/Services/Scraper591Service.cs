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
        // Step 1: Get CSRF token from homepage
        var initReq = new HttpRequestMessage(HttpMethod.Get, "https://rent.591.com.tw/");
        initReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        initReq.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        initReq.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9");
        var initResp = await httpClient.SendAsync(initReq);
        var html = await initResp.Content.ReadAsStringAsync();

        var csrfToken = "";
        var metaMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"<meta\s+name=""csrf-token""\s+content=""([^""]+)""");
        if (metaMatch.Success) csrfToken = metaMatch.Groups[1].Value;
        if (string.IsNullOrEmpty(csrfToken))
            csrfToken = cookieContainer?.GetCookies(new Uri("https://rent.591.com.tw/"))["T591_TOKEN"]?.Value ?? "";

        Console.WriteLine($"[Debug] csrfToken={csrfToken[..Math.Min(csrfToken.Length, 10)]}...");

        var sections = string.Join(",",
            config.Districts
                .Where(d => DistrictCodes.ContainsKey(d))
                .Select(d => DistrictCodes[d]));

        var types = string.Join(",",
            config.RoomTypes
                .Where(t => RoomTypeCodes.ContainsKey(t))
                .Select(t => RoomTypeCodes[t]));

        // Debug: print all cookies CookieContainer has for rent.591.com.tw
        if (cookieContainer != null)
        {
            var jar = cookieContainer.GetCookies(new Uri("https://rent.591.com.tw/"));
            Console.WriteLine($"[Debug] cookies in jar: {string.Join(", ", jar.Cast<System.Net.Cookie>().Select(c => c.Name))}");
            // Prefer T591_TOKEN from jar over meta tag
            var jarToken = jar["T591_TOKEN"]?.Value;
            if (!string.IsNullOrEmpty(jarToken)) csrfToken = jarToken;
        }
        Console.WriteLine($"[Debug] final csrfToken={csrfToken[..Math.Min(csrfToken.Length, 10)]}...");

        // Step 2: GET rsList — cookies sent automatically by CookieContainer, CSRF token in header
        var url = $"https://rent.591.com.tw/home/search/rsList" +
                  $"?is_new_list=1&type={types}&region=1&section={sections}" +
                  $"&price=0_{config.MaxPrice}&order=posttime&orderType=desc&firstRow=0&totalRows=30";

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        req.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        req.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9");
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Add("X-CSRF-Token", csrfToken);
        req.Headers.Add("Referer", "https://rent.591.com.tw/");

        var response = await httpClient.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Debug] search → {(int)response.StatusCode}: {body[..Math.Min(body.Length, 200)]}");

        if (!response.IsSuccessStatusCode) return new List<SearchItem>();

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<SearchResponse>(
            new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));

        if (result?.Status != "1") return new List<SearchItem>();

        return result.Data.Items
            .Where(item => double.TryParse(item.Area, out var area) && area >= config.MinSizePing)
            .ToList();
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
