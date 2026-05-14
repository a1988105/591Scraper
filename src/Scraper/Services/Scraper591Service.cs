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
        var itemMatches = Regex.Matches(html, @"class=""item""\s+data-id=""(\d+)""");
        var items = new List<SearchItem>();

        for (var i = 0; i < itemMatches.Count; i++)
        {
            var postId = itemMatches[i].Groups[1].Value;
            var blockStart = itemMatches[i].Index;
            var blockEnd = i + 1 < itemMatches.Count ? itemMatches[i + 1].Index : html.Length;
            var block = html[blockStart..blockEnd];

            var titleMatch = Regex.Match(block, @"title=""([^""]+)""");
            var title = titleMatch.Success ? titleMatch.Groups[1].Value : "";

            var priceMatch = Regex.Match(block, @"class=""price font-arial""[^>]*>([0-9,]+)");
            var price = priceMatch.Success ? priceMatch.Groups[1].Value.Replace(",", "") : "0";

            var kindName = KnownKinds.FirstOrDefault(kind =>
                Regex.IsMatch(block, $@">{Regex.Escape(kind)}<")) ?? "";

            var sizeMatch = Regex.Match(
                block,
                @"class=""line""[^>]*>.*?>(\d+\.?\d*)",
                RegexOptions.Singleline);
            var area = sizeMatch.Success ? sizeMatch.Groups[1].Value : "0";

            var addressMatches = Regex.Matches(block, @">([^<]*-[^<]+)<");
            var address = addressMatches.Count > 0
                ? addressMatches[addressMatches.Count - 1].Groups[1].Value.Trim()
                : "";

            if (!double.TryParse(area, out var sizePing) || sizePing < config.MinSizePing)
                continue;

            items.Add(new SearchItem
            {
                PostId = postId,
                Title = title,
                Price = price,
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
