using System.Text.Json.Serialization;

namespace Scraper.Models;

// ── Domain model ────────────────────────────────────────────────
public class Listing
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = default!;

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lng")]
    public double? Lng { get; set; }

    [JsonPropertyName("size_ping")]
    public double SizePing { get; set; }

    [JsonPropertyName("room_type")]
    public string RoomType { get; set; } = default!;

    [JsonPropertyName("has_furniture")]
    public bool HasFurniture { get; set; }

    [JsonPropertyName("has_natural_gas")]
    public bool HasNaturalGas { get; set; }

    [JsonPropertyName("has_cable_tv")]
    public bool HasCableTv { get; set; }

    [JsonPropertyName("has_internet")]
    public bool HasInternet { get; set; }

    [JsonPropertyName("has_parking")]
    public bool HasParking { get; set; }

    [JsonPropertyName("pet_allowed")]
    public bool PetAllowed { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();

    [JsonPropertyName("scraped_at")]
    public DateTimeOffset ScrapedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("notified")]
    public bool Notified { get; set; }
}

// ── 591 Search API response DTOs ────────────────────────────────
// NOTE: 591 的 API 為非官方 API，欄位名稱可能隨時變動。
public class SearchResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;

    [JsonPropertyName("data")]
    public SearchData Data { get; set; } = default!;
}

public class SearchData
{
    [JsonPropertyName("data")]
    public List<SearchItem> Items { get; set; } = new();

    [JsonPropertyName("records")]
    public string Records { get; set; } = "0";
}

public class SearchItem
{
    [JsonPropertyName("post_id")]
    public string PostId { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("address")]
    public string Address { get; set; } = default!;

    [JsonPropertyName("area")]
    public string Area { get; set; } = "0";

    [JsonPropertyName("kind_name")]
    public string KindName { get; set; } = default!;

    [JsonPropertyName("photo")]
    public string Photo { get; set; } = default!;
}

// ── 591 Detail API response DTOs ────────────────────────────────
public class DetailResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("data")]
    public DetailData Data { get; set; } = default!;
}

public class DetailData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("photo_list")]
    public List<PhotoItem> PhotoList { get; set; } = new();

    [JsonPropertyName("furniture")]
    public int Furniture { get; set; }

    [JsonPropertyName("natural_gas")]
    public int NaturalGas { get; set; }

    [JsonPropertyName("cable_tv")]
    public int CableTv { get; set; }

    [JsonPropertyName("broadband")]
    public int Broadband { get; set; }

    [JsonPropertyName("parking_space")]
    public int ParkingSpace { get; set; }

    [JsonPropertyName("can_keep_pet")]
    public int CanKeepPet { get; set; }
}

public class PhotoItem
{
    [JsonPropertyName("src")]
    public string Src { get; set; } = default!;
}

// ── Geocoding API response ──────────────────────────────────────
public class GeocodingResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;

    [JsonPropertyName("results")]
    public List<GeocodingResult> Results { get; set; } = new();
}

public class GeocodingResult
{
    [JsonPropertyName("geometry")]
    public GeoGeometry Geometry { get; set; } = default!;
}

public class GeoGeometry
{
    [JsonPropertyName("location")]
    public GeoLocation Location { get; set; } = default!;
}

public class GeoLocation
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}
