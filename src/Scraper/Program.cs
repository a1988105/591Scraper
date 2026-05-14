using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Config;
using Scraper.Models;
using Scraper.Services;

EnvFileLoader.LoadIfExists(
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"));

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("config.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var config = configuration.Get<ScraperConfig>()
    ?? throw new InvalidOperationException("config.json is missing or invalid.");

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new InvalidOperationException("SUPABASE_URL is required.");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new InvalidOperationException("SUPABASE_KEY is required.");
var telegramToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is required.");
var telegramChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
    ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID is required.");

var services = new ServiceCollection();
services.AddHttpClient();
var provider = services.BuildServiceProvider();
var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

var scraper591 = new Scraper591Service(httpFactory.CreateClient());
var geocoding = new GeocodingService(httpFactory.CreateClient());
var supabase = new SupabaseService(httpFactory.CreateClient(), supabaseUrl, supabaseKey);
var telegram = new TelegramService(httpFactory.CreateClient());

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Start scraping 591");

var searchItems = await scraper591.SearchListingsAsync(config);
Console.WriteLine($"Fetched listings: {searchItems.Count}");

if (searchItems.Count == 0) return;

var candidateIds = searchItems.Select(x => x.PostId).ToList();
var existingIds = await supabase.GetExistingIdsAsync(candidateIds);
var newItems = searchItems.Where(x => !existingIds.Contains(x.PostId)).ToList();
Console.WriteLine($"New listings after dedupe: {newItems.Count}");

if (newItems.Count == 0) return;

var newListings = new List<Listing>();
var detailUnavailableCount = 0;
var filteredByFurnitureCount = 0;
var filteredByInternetCount = 0;

foreach (var item in newItems)
{
    var detail = await scraper591.GetListingDetailAsync(item.PostId);
    var detailFetched = detail is not null;
    if (!detailFetched)
        detailUnavailableCount++;

    var coords = await geocoding.GetCoordinatesAsync(item.Address);
    await Task.Delay(1100); // Nominatim rate limit: 1 req/sec
    Console.WriteLine($"Processing {item.Title} ({item.PostId}) - address: {item.Address}");

    var listing = new Listing
    {
        Id = item.PostId,
        Title = item.Title,
        Price = int.TryParse(item.Price, out var p) ? p : 0,
        Address = item.Address,
        Lat = coords?.Lat,
        Lng = coords?.Lng,
        SizePing = double.TryParse(item.Area, out var a) ? a : 0,
        RoomType = item.KindName,
        HasFurniture = detail?.Furniture == 1,
        HasNaturalGas = detail?.NaturalGas == 1,
        HasCableTv = detail?.CableTv == 1,
        HasInternet = detail?.Broadband == 1,
        HasParking = detail?.ParkingSpace == 1,
        PetAllowed = detail?.CanKeepPet == 1,
        Url = $"https://rent.591.com.tw/rent-detail-{item.PostId}.html",
        Images = detail?.PhotoList.Select(ph => ph.Src).ToList()
                 ?? (item.Photo != "" ? [item.Photo] : []),
        ScrapedAt = DateTimeOffset.UtcNow,
        Notified = false
    };

    if (!PriceFilter.IsWithinRange(item.Price, config))
        continue;

    if (config.RequireFurniture && detailFetched && !listing.HasFurniture)
    {
        filteredByFurnitureCount++;
        continue;
    }

    if (config.RequireInternet && detailFetched && !listing.HasInternet)
    {
        filteredByInternetCount++;
        continue;
    }

    if (!EquipmentFilter.ShouldInclude(config, listing, detailFetched))
        continue;

    newListings.Add(listing);
}

Console.WriteLine($"Passed equipment filter: {newListings.Count}");
Console.WriteLine($"Detail fetch failed: {detailUnavailableCount}");
Console.WriteLine($"Filtered by furniture: {filteredByFurnitureCount}");
Console.WriteLine($"Filtered by internet: {filteredByInternetCount}");

if (newListings.Count == 0) return;

await supabase.UpsertListingsAsync(newListings);
Console.WriteLine("Upserted to Supabase");

var notifiedIds = new List<string>();
foreach (var listing in newListings)
{
    try
    {
        await telegram.SendNotificationAsync(listing, telegramToken, telegramChatId);
        notifiedIds.Add(listing.Id);
        Console.WriteLine($"Notified: {listing.Title} (${listing.Price:N0})");
        await Task.Delay(500);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Telegram failed [{listing.Id}]: {ex.Message}");
    }
}

if (notifiedIds.Count > 0)
    await supabase.MarkNotifiedAsync(notifiedIds);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Done. Notified: {notifiedIds.Count}");
