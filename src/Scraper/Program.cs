using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Config;
using Scraper.Models;
using Scraper.Services;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("config.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var config = configuration.Get<ScraperConfig>()
    ?? throw new InvalidOperationException("config.json 讀取失敗");

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new InvalidOperationException("SUPABASE_URL 環境變數未設定");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new InvalidOperationException("SUPABASE_KEY 環境變數未設定");
var telegramToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN 環境變數未設定");
var telegramChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
    ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID 環境變數未設定");
var googleMapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
    ?? throw new InvalidOperationException("GOOGLE_MAPS_API_KEY 環境變數未設定");

var services = new ServiceCollection();
services.AddHttpClient();
var provider = services.BuildServiceProvider();
var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

// Scraper591 needs a CookieContainer so cookies from the init request are auto-sent on subsequent requests
var scraperHandler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
var scraper591 = new Scraper591Service(new HttpClient(scraperHandler));
var geocoding = new GeocodingService(httpFactory.CreateClient());
var supabase = new SupabaseService(httpFactory.CreateClient(), supabaseUrl, supabaseKey);
var telegram = new TelegramService(httpFactory.CreateClient());

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 591 爬蟲啟動");

// Step 1: 搜尋 591
var searchItems = await scraper591.SearchListingsAsync(config);
Console.WriteLine($"找到 {searchItems.Count} 筆符合條件的物件");

if (searchItems.Count == 0) return;

// Step 2: 過濾已存在
var candidateIds = searchItems.Select(x => x.PostId).ToList();
var existingIds = await supabase.GetExistingIdsAsync(candidateIds);
var newItems = searchItems.Where(x => !existingIds.Contains(x.PostId)).ToList();
Console.WriteLine($"其中 {newItems.Count} 筆為新物件");

if (newItems.Count == 0) return;

// Step 3: 抓取詳細資訊 + 組裝 Listing
var newListings = new List<Listing>();
foreach (var item in newItems)
{
    var detail = await scraper591.GetListingDetailAsync(item.PostId);
    var coords = await geocoding.GetCoordinatesAsync(item.Address, googleMapsKey);

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
                 ?? (item.Photo != "" ? new List<string> { item.Photo } : new List<string>()),
        ScrapedAt = DateTimeOffset.UtcNow,
        Notified = false
    };

    if (config.RequireFurniture && !listing.HasFurniture) continue;
    if (config.RequireInternet && !listing.HasInternet) continue;

    newListings.Add(listing);
}

Console.WriteLine($"通過設備篩選：{newListings.Count} 筆");

if (newListings.Count == 0) return;

// Step 4: Upsert to Supabase
await supabase.UpsertListingsAsync(newListings);
Console.WriteLine("已寫入 Supabase");

// Step 5: Send Telegram notifications
var notifiedIds = new List<string>();
foreach (var listing in newListings)
{
    try
    {
        await telegram.SendNotificationAsync(listing, telegramToken, telegramChatId);
        notifiedIds.Add(listing.Id);
        Console.WriteLine($"已推送：{listing.Title} (${listing.Price:N0})");
        await Task.Delay(500); // Telegram rate limit
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Telegram 推送失敗 [{listing.Id}]: {ex.Message}");
    }
}

// Step 6: Mark notified
if (notifiedIds.Count > 0)
    await supabase.MarkNotifiedAsync(notifiedIds);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 完成。推送 {notifiedIds.Count} 筆。");
