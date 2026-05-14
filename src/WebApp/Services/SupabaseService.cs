using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WebApp.Models;

namespace WebApp.Services;

public class SupabaseService(HttpClient httpClient, string supabaseUrl, string supabaseKey)
{
    private void AddHeaders(HttpRequestMessage req)
    {
        req.Headers.Add("apikey", supabaseKey);
        req.Headers.Add("Authorization", $"Bearer {supabaseKey}");
    }

    public async Task<List<Listing>> GetListingsAsync(
        bool? hasFurniture = null,
        bool? hasInternet = null,
        bool? hasNaturalGas = null,
        bool? hasParking = null,
        bool? petAllowed = null,
        int? maxPrice = null)
    {
        var filters = new List<string>();
        if (hasFurniture == true) filters.Add("has_furniture=eq.true");
        if (hasInternet == true) filters.Add("has_internet=eq.true");
        if (hasNaturalGas == true) filters.Add("has_natural_gas=eq.true");
        if (hasParking == true) filters.Add("has_parking=eq.true");
        if (petAllowed == true) filters.Add("pet_allowed=eq.true");
        if (maxPrice.HasValue) filters.Add($"price=lte.{maxPrice}");

        var query = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
        var url = $"{supabaseUrl}/rest/v1/listings?order=scraped_at.desc{query}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(req);
        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<Listing>>() ?? new();
    }

    public async Task<List<Favorite>> GetFavoritesWithListingsAsync()
    {
        var url = $"{supabaseUrl}/rest/v1/favorites?select=*,listing:listings(*)&order=favorited_at.desc";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(req);
        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<Favorite>>() ?? new();
    }

    public async Task AddFavoriteAsync(string listingId)
    {
        var url = $"{supabaseUrl}/rest/v1/favorites";
        var body = JsonSerializer.Serialize(new
        {
            listing_id = listingId,
            note = "",
            status = "待看"
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddHeaders(req);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveFavoriteAsync(string listingId)
    {
        var url = $"{supabaseUrl}/rest/v1/favorites?listing_id=eq.{listingId}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        AddHeaders(req);
        req.Headers.Add("Prefer", "return=minimal");

        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateFavoriteAsync(string listingId, FavoriteUpdateRequest update)
    {
        var url = $"{supabaseUrl}/rest/v1/favorites?listing_id=eq.{listingId}";
        var patch = new Dictionary<string, object?>();
        if (update.Note != null) patch["note"] = update.Note;
        if (update.Status != null) patch["status"] = update.Status;

        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        AddHeaders(req);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(
            JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();
    }
}
