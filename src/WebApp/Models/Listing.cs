using System.Text.Json.Serialization;

namespace WebApp.Models;

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

    [JsonPropertyName("has_fridge")]
    public bool HasFridge { get; set; }

    [JsonPropertyName("has_washing_machine")]
    public bool HasWashingMachine { get; set; }

    [JsonPropertyName("has_water_heater")]
    public bool HasWaterHeater { get; set; }

    [JsonPropertyName("has_air_con")]
    public bool HasAirCon { get; set; }

    [JsonPropertyName("has_tv")]
    public bool HasTv { get; set; }

    [JsonPropertyName("has_bed")]
    public bool HasBed { get; set; }

    [JsonPropertyName("has_wardrobe")]
    public bool HasWardrobe { get; set; }

    [JsonPropertyName("has_elevator")]
    public bool HasElevator { get; set; }

    [JsonPropertyName("has_balcony")]
    public bool HasBalcony { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();

    [JsonPropertyName("scraped_at")]
    public DateTimeOffset ScrapedAt { get; set; }
}
