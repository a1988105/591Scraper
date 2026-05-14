using System.Net;
using System.Net.Http.Json;
using Scraper.Models;

namespace Scraper.Services;

public class GeocodingService(HttpClient httpClient)
{
    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string address, string apiKey)
    {
        if (!TryBuildGeocodingUri(address, apiKey, out var uri))
            return null;

        var response = await httpClient.GetFromJsonAsync<GeocodingResponse>(uri);
        if (response?.Status != "OK" || response.Results.Count == 0) return null;
        var loc = response.Results[0].Geometry.Location;
        return (loc.Lat, loc.Lng);
    }

    internal static bool TryBuildGeocodingUri(string address, string apiKey, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(apiKey))
            return false;

        var encoded = WebUtility.UrlEncode(address.Trim());
        var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encoded}&key={apiKey}&language=zh-TW";

        if (url.Length > 2048)
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            return false;

        return true;
    }
}
