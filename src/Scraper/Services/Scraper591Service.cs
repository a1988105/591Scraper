using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Scraper.Config;
using Scraper.Models;

namespace Scraper.Services;

public class Scraper591Service(HttpClient httpClient)
{
    private static readonly Dictionary<string, string> DistrictCodes = new()
    {
        ["中正區"] = "1",
        ["大同區"] = "2",
        ["中山區"] = "3",
        ["松山區"] = "4",
        ["萬華區"] = "5",
        ["信義區"] = "6",
        ["大安區"] = "7",
        ["士林區"] = "8",
        ["北投區"] = "9",
        ["內湖區"] = "10",
        ["南港區"] = "11",
        ["文山區"] = "12"
    };

    private static readonly Dictionary<string, string> RoomTypeCodes = new()
    {
        ["整層住家"] = "1",
        ["獨立套房"] = "2",
        ["雅房"] = "3",
        ["分租套房"] = "8"
    };

    private static readonly string[] KnownKinds = ["整層住家", "獨立套房", "雅房", "分租套房"];

    internal static string BuildSearchUrl(ScraperConfig config, int? page = null)
    {
        var sections = ResolveSectionCodes(config);
        var kinds = string.Join(",",
            config.RoomTypes
                .Where(t => RoomTypeCodes.ContainsKey(t))
                .Select(t => RoomTypeCodes[t]));

        var queryParts = new List<string>
        {
            $"region={config.Region}",
            $"price={config.MinPrice}_{config.MaxPrice}",
            "order=posttime",
            "orderType=desc"
        };

        if (!string.IsNullOrWhiteSpace(sections))
            queryParts.Add($"section={sections}");

        if (!string.IsNullOrWhiteSpace(kinds))
            queryParts.Add($"kind={kinds}");

        if (page is > 1)
        {
            queryParts.Add($"page={page.Value}");
            queryParts.Add($"firstRow={(page.Value - 1) * 30}");
        }

        return $"https://rent.591.com.tw/list?{string.Join("&", queryParts)}";
    }

    internal static string ResolveSectionCodes(ScraperConfig config)
    {
        if (config.SectionCodes.Count > 0)
            return string.Join(",", config.SectionCodes);

        var mappedSections = config.Districts
            .Where(d => DistrictCodes.ContainsKey(d))
            .Select(d => DistrictCodes[d])
            .ToList();

        if (config.Districts.Count > 0 && mappedSections.Count == 0)
        {
            throw new InvalidOperationException(
                $"Districts could not be mapped for region {config.Region}: {string.Join(", ", config.Districts)}. " +
                "Use SectionCodes for non-Taipei regions or fix the district names.");
        }

        return string.Join(",", mappedSections);
    }

    public async Task<List<SearchItem>> SearchListingsAsync(ScraperConfig config)
    {
        var items = new List<SearchItem>();
        var seenIds = new HashSet<string>();

        for (var page = 1; ; page++)
        {
            var url = BuildSearchUrl(config, page);
            Console.WriteLine($"Fetching page {page}: {url}");

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            req.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            req.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9");

            var response = await httpClient.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Page {page} request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                break;
            }

            var html = await response.Content.ReadAsStringAsync();
            var rawItemCount = CountListingMatches(html);
            Console.WriteLine(
                $"Page {page} response: status={(int)response.StatusCode}, htmlLength={html.Length}, title='{ExtractPageTitle(html)}', rawItemMatches={rawItemCount}");
            var pageItems = ParseHtmlListings(html, config);
            Console.WriteLine($"Page {page} parsed items: {pageItems.Count}");
            if (pageItems.Count == 0)
            {
                Console.WriteLine($"Page {page} preview: {BuildHtmlPreview(html)}");
                break;
            }

            var addedThisPage = 0;
            foreach (var item in pageItems)
            {
                if (seenIds.Add(item.PostId))
                {
                    items.Add(item);
                    addedThisPage++;
                }
            }

            if (addedThisPage == 0)
                break;

            Console.WriteLine($"Page {page} new items: {addedThisPage}");
        }

        return items;
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

            var price = ExtractPrice(block);

            var kindName = KnownKinds.FirstOrDefault(kind =>
                Regex.IsMatch(block, $@">{Regex.Escape(kind)}<")) ?? "";

            var sizeMatch = Regex.Match(
                block,
                @"class=""line""[^>]*>.*?>(\d+\.?\d*)",
                RegexOptions.Singleline);
            var area = sizeMatch.Success ? sizeMatch.Groups[1].Value : "0";

            var infoTexts = Regex.Matches(
                    block,
                    @"class=""item-info-txt""[^>]*>(.*?)</div>",
                    RegexOptions.Singleline)
                .Select(m => Regex.Replace(m.Groups[1].Value!, "<[^>]+>", " "))
                .Select(WebUtility.HtmlDecode)
                .Select(text => Regex.Replace(text!, @"\s+", " ").Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            var address = infoTexts.Count > 1 ? infoTexts[1] : "";

            if (!double.TryParse(area, out var sizePing) || sizePing < config.MinSizePing)
                continue;

            if (!PriceFilter.IsWithinRange(price, config))
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

    internal static string ExtractPageTitle(string html)
    {
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!titleMatch.Success)
            return "";

        var title = Regex.Replace(titleMatch.Groups[1].Value, "<[^>]+>", " ");
        title = WebUtility.HtmlDecode(title);
        return Regex.Replace(title, @"\s+", " ").Trim();
    }

    internal static int CountListingMatches(string html)
        => Regex.Matches(html, @"class=""item""\s+data-id=""(\d+)""").Count;

    internal static string BuildHtmlPreview(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var preview = Regex.Replace(html, "<[^>]+>", " ");
        preview = WebUtility.HtmlDecode(preview);
        preview = Regex.Replace(preview, @"\s+", " ").Trim();
        return preview.Length <= 200 ? preview : preview[..200];
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

    private static string ExtractPrice(string block)
    {
        var itemInfoPriceMatch = Regex.Match(
            block,
            @"class=""item-info-price""[^>]*>(.*?)</div>\s*</div>",
            RegexOptions.Singleline);
        var itemInfoPrice = ExtractFirstNumber(itemInfoPriceMatch.Groups[1].Value);
        if (!string.IsNullOrWhiteSpace(itemInfoPrice))
            return itemInfoPrice;

        var legacyPriceMatch = Regex.Match(
            block,
            @"class=""price font-arial""[^>]*>(.*?)</(?:span|div)>",
            RegexOptions.Singleline);
        var legacyPrice = ExtractFirstNumber(legacyPriceMatch.Groups[1].Value);
        if (!string.IsNullOrWhiteSpace(legacyPrice))
            return legacyPrice;

        return "0";
    }

    private static string ExtractFirstNumber(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        var numberMatch = Regex.Match(text, @"\d[\d,]*");
        return numberMatch.Success ? numberMatch.Value.Replace(",", "") : "";
    }
}
