using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Scraper.Models;

namespace Scraper.Services;

public class SupabaseService(HttpClient httpClient, string supabaseUrl, string supabaseKey)
{
    private void AddHeaders(HttpRequestMessage req)
    {
        req.Headers.Add("apikey", supabaseKey);
        req.Headers.Add("Authorization", $"Bearer {supabaseKey}");
    }

    public async Task<HashSet<string>> GetExistingIdsAsync(IEnumerable<string> ids)
    {
        var idList = string.Join(",", ids.Select(id => $"\"{id}\""));
        var url = $"{supabaseUrl}/rest/v1/listings?id=in.({idList})&select=id";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(req);
        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<IdOnly>>()
                    ?? new List<IdOnly>();
        return items.Select(x => x.Id).ToHashSet();
    }

    public async Task UpsertListingsAsync(IReadOnlyList<Listing> listings)
    {
        if (listings.Count == 0) return;

        var url = $"{supabaseUrl}/rest/v1/listings";
        var json = JsonSerializer.Serialize(listings);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddHeaders(req);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkNotifiedAsync(IEnumerable<string> ids)
    {
        var idList = string.Join(",", ids.Select(id => $"\"{id}\""));
        var url = $"{supabaseUrl}/rest/v1/listings?id=in.({idList})";
        var body = JsonSerializer.Serialize(new { notified = true });

        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        AddHeaders(req);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(req);
        response.EnsureSuccessStatusCode();
    }

    private class IdOnly
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = default!;
    }
}
