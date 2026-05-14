using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Scraper.Services;

public class GeocodingService(HttpClient httpClient)
{
    private const string UserAgent = "rental-monitor/1.0";

    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

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
}

internal class NominatimResult
{
    [JsonPropertyName("lat")]
    public string Lat { get; set; } = default!;

    [JsonPropertyName("lon")]
    public string Lon { get; set; } = default!;
}
