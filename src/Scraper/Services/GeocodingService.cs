using System.Net;
using System.Net.Http.Json;
using Scraper.Models;

namespace Scraper.Services;

public class GeocodingService(HttpClient httpClient)
{
    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string address, string apiKey)
    {
        var encoded = WebUtility.UrlEncode(address);
        var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encoded}&key={apiKey}&language=zh-TW";
        var response = await httpClient.GetFromJsonAsync<GeocodingResponse>(url);
        if (response?.Status != "OK" || response.Results.Count == 0) return null;
        var loc = response.Results[0].Geometry.Location;
        return (loc.Lat, loc.Lng);
    }
}
