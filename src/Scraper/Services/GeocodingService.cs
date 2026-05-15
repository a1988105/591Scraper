using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Scraper.Services;

public class GeocodingService(HttpClient httpClient)
{
    private const string UserAgent = "rental-monitor/1.0";

    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var cleaned = NormalizeAddress(address);

        var result = await CallNominatimAsync(cleaned);
        if (result.HasValue) return result;

        foreach (var fallback in ExtractFallbackLevels(cleaned))
        {
            await Task.Delay(1100);
            result = await CallNominatimAsync(fallback);
            if (result.HasValue) return result;
        }

        return null;
    }

    private async Task<(double Lat, double Lng)?> CallNominatimAsync(string address)
    {
        var encoded = Uri.EscapeDataString(address.Trim());
        var url = $"https://nominatim.openstreetmap.org/search?q={encoded}&format=json&limit=1&accept-language=zh-TW";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", UserAgent);

        var response = await httpClient.SendAsync(req);
        if (!response.IsSuccessStatusCode) return null;

        var results = await response.Content.ReadFromJsonAsync<List<NominatimResult>>();
        if (results is null || results.Count == 0) return null;

        if (!double.TryParse(results[0].Lat, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(results[0].Lon, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lng))
            return null;

        return (lat, lng);
    }

    internal static string NormalizeAddress(string address)
    {
        // Remove parenthesized content (floor descriptions, building names)
        address = Regex.Replace(address, @"[（(][^)）]*[）)]", "");

        // Remove content after slash (half-width or full-width)
        var slash = address.IndexOfAny(['/', '／']);
        if (slash >= 0) address = address[..slash];

        // Remove trailing floor descriptions: 3F, 3f, 3樓, B1F, B1
        address = Regex.Replace(address, @"\s*([Bb]\d+[Ff]?|\d+[Ff樓])$", "");

        // Remove 近XXX landmark descriptions
        address = Regex.Replace(address, @"近\S+", "");

        // Remove trailing non-address descriptive words
        address = Regex.Replace(address,
            @"[\s,，、]*(精緻|優質|全新|整層住家|獨立套房|分租套房|雅房|套房|公寓|大廈|華廈)+$", "");

        address = address.Trim();
        return AddCityPrefix(address);
    }

    // Scope: Taipei City and New Taipei City only.
    // Addresses from other cities geocode without district-level city prefix injection.
    private static readonly Dictionary<string, string> DistrictToCity = new()
    {
        // 台北市 12 區
        ["中正區"] = "台北市", ["大同區"] = "台北市", ["中山區"] = "台北市",
        ["松山區"] = "台北市", ["大安區"] = "台北市", ["萬華區"] = "台北市",
        ["信義區"] = "台北市", ["士林區"] = "台北市", ["北投區"] = "台北市",
        ["內湖區"] = "台北市", ["南港區"] = "台北市", ["文山區"] = "台北市",
        // 新北市 29 區
        ["板橋區"] = "新北市", ["三重區"] = "新北市", ["中和區"] = "新北市",
        ["永和區"] = "新北市", ["新莊區"] = "新北市", ["新店區"] = "新北市",
        ["樹林區"] = "新北市", ["鶯歌區"] = "新北市", ["三峽區"] = "新北市",
        ["淡水區"] = "新北市", ["汐止區"] = "新北市", ["瑞芳區"] = "新北市",
        ["土城區"] = "新北市", ["蘆洲區"] = "新北市", ["五股區"] = "新北市",
        ["泰山區"] = "新北市", ["林口區"] = "新北市", ["深坑區"] = "新北市",
        ["石碇區"] = "新北市", ["坪林區"] = "新北市", ["三芝區"] = "新北市",
        ["石門區"] = "新北市", ["八里區"] = "新北市", ["平溪區"] = "新北市",
        ["雙溪區"] = "新北市", ["貢寮區"] = "新北市", ["金山區"] = "新北市",
        ["萬里區"] = "新北市", ["烏來區"] = "新北市",
    };

    internal static IEnumerable<string> ExtractFallbackLevels(string address)
    {
        var match = Regex.Match(address,
            @"^(?<city>台北市|新北市|桃園市|台中市|台南市|高雄市|基隆市|新竹市|嘉義市|" +
            @"宜蘭縣|花蓮縣|台東縣|屏東縣|南投縣|雲林縣|嘉義縣|彰化縣|苗栗縣|新竹縣|" +
            @"連江縣|金門縣|澎湖縣|臺北市|臺中市|臺南市|臺東縣)?" +
            @"(?<district>[^\s]+?[區鄉鎮市])" +
            @"(?<road>[^\s]+?(?:路|街|大道|巷|弄))" +
            @"(?<section>第?[一二三四五六七八九十\d]+段)?" +
            @"(?<number>\d+號)?");

        if (!match.Success) yield break;

        var city     = match.Groups["city"].Value;
        var district = match.Groups["district"].Value;
        var road     = match.Groups["road"].Value;
        var section  = match.Groups["section"].Value;
        var number   = match.Groups["number"].Value;

        // Level 2: structured rebuild — only emit if different from input
        var level2 = city + district + road + section + number;
        if (level2 != address) yield return level2;

        // Level 3: road-level (drop number) — only emit if there was a number to drop
        if (!string.IsNullOrEmpty(number))
            yield return city + district + road + section;
    }

    private static string AddCityPrefix(string address)
    {
        if (Regex.IsMatch(address,
            @"^(台北市|新北市|桃園市|台中市|台南市|高雄市|基隆市|新竹市|嘉義市|" +
            @"宜蘭縣|花蓮縣|台東縣|屏東縣|南投縣|雲林縣|嘉義縣|彰化縣|苗栗縣|新竹縣|" +
            @"連江縣|金門縣|澎湖縣|臺北市|臺中市|臺南市|臺東縣)"))
            return address;

        foreach (var (district, city) in DistrictToCity)
        {
            if (address.StartsWith(district))
                return city + address;
        }

        return address;
    }
}

internal class NominatimResult
{
    [JsonPropertyName("lat")]
    public string Lat { get; set; } = default!;

    [JsonPropertyName("lon")]
    public string Lon { get; set; } = default!;
}
