using System.Text.Json.Serialization;

namespace WebApp.Models;

public class Favorite
{
    [JsonPropertyName("listing_id")]
    public string ListingId { get; set; } = default!;

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "待看";

    [JsonPropertyName("favorited_at")]
    public DateTimeOffset FavoritedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("listing")]
    public Listing? Listing { get; set; }
}

public class FavoriteUpdateRequest
{
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
