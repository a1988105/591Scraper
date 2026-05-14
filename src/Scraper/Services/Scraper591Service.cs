using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Scraper.Config;
using Scraper.Models;

namespace Scraper.Services;

public class Scraper591Service(HttpClient httpClient)
{
    private static readonly Dictionary<string, string> DistrictCodes = new()
    {
        ["中正區"] = "1", ["大同區"] = "2", ["中山區"] = "3", ["松山區"] = "4",
        ["大安區"] = "7", ["萬華區"] = "5", ["信義區"] = "6", ["士林區"] = "8",
        ["北投區"] = "9", ["內湖區"] = "10", ["南港區"] = "11", ["文山區"] = "12"
    };

    // kind URL codes: 整層住家=1, 獨立套房=2, 雅房=3, 分租套房=8
    private static readonly Dictionary<string, string> RoomTypeCodes = new()
    {
        ["整層住家"] = "1", ["獨立套房"] = "2", ["雅房"] = "3", ["分租套房"] = "8"
    };

    private static readonly string[] KnownKinds = ["整層住家", "獨立套房", "雅房", "分租套房"];

    public async Task<List<SearchItem>> SearchListingsAsync(ScraperConfig config)
    {
        var sections = string.Join(",",
            config.Districts
                .Where(d => DistrictCodes.ContainsKey(d))
                .Select(d => DistrictCodes[d]));

        var kinds = string.Join(",",
            config.RoomTypes
                .Where(t => RoomTypeCodes.ContainsKey(t))
                .Select(t => RoomTypeCodes[t]));

        var url = $"https://rent.591.com.tw/list?region=1&section={sections}&kind={kinds}" +
                  $"&price=0_{config.MaxPrice}&order=posttime&orderType=desc";

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        req.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        req.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9");

        var response = await httpClient.SendAsync(req);
        if (!response.IsSuccessStatusCode) return new List<SearchItem>();

        var html = await response.Content.ReadAsStringAsync();
        return ParseHtmlListings(html, config);
    }

    internal static List<SearchItem> ParseHtmlListings(string html, ScraperConfig config)
    {
        // Each listing card: <div class="item" data-id="12345" ...>
        var ids = Regex.Matches(html, @"class=""item[^""]*""[^>]*data-id=""(\d+)""")
            .Select(m => m.Groups[1].Value).ToList();

        var titles = Regex.Matches(html, @"class=""item-info-title""[^>]*>([^<]+)")
            .Select(m => m.Groups[1].Value.Trim()).ToList();

        var prices = Regex.Matches(html, @"class=""price font-arial""[^>]*>([^<]+)")
            .Select(m => m.Groups[1].Value.Replace(",", "").Trim()).ToList();

        // Comes in pairs per listing: [0]=種類+坪數, [1]=完整地址, [2]=種類+坪數, [3]=完整地址, ...
        var infoTxts = Regex.Matches(html, @"class=""item-info-txt""[^>]*>([^<]+)")
            .Select(m => m.Groups[1].Value.Trim()).ToList();

        var count = Math.Min(ids.Count, Math.Min(titles.Count, prices.Count));
        var items = new List<SearchItem>();

        for (var i = 0; i < count; i++)
        {
            var kindTxt = i * 2 < infoTxts.Count ? infoTxts[i * 2] : "";
            var address = i * 2 + 1 < infoTxts.Count ? infoTxts[i * 2 + 1] : "";

            var kindName = KnownKinds.FirstOrDefault(k => kindTxt.StartsWith(k)) ?? kindTxt;

            var sizeMatch = Regex.Match(kindTxt, @"(\d+\.?\d*)坪");
            var area = sizeMatch.Success ? sizeMatch.Groups[1].Value : "0";

            if (!double.TryParse(area, out var sizePing) || sizePing < config.MinSizePing)
                continue;

            items.Add(new SearchItem
            {
                PostId = ids[i],
                Title = titles[i],
                Price = prices[i],
                Address = address,
                Area = area,
                KindName = kindName,
                Photo = ""
            });
        }

        return items;
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
