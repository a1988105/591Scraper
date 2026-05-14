# 591 租屋追蹤系統 — Plan 2: Web App（地圖介面）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 ASP.NET Core Web App，提供 REST API 讀寫 Supabase，前端用 Google Maps 顯示收藏物件，支援右側面板篩選與收藏管理。

**Architecture:** ASP.NET Core 8 Minimal API + 靜態檔案服務（wwwroot）。API 直接呼叫 Supabase REST API。前端純 HTML/JS，Google Maps JavaScript API 渲染地圖。部署至 Railway，環境變數注入 secrets。

**Tech Stack:** .NET 8 ASP.NET Core, System.Text.Json, Google Maps JavaScript API, xUnit, Railway (Dockerfile 部署)

**前置條件：** Plan 1 已完成（Supabase 兩張表已建立、有資料）

---

## File Map

| 檔案 | 職責 |
|------|------|
| `src/WebApp/WebApp.csproj` | 專案設定 |
| `src/WebApp/Program.cs` | 進入點、路由、DI、靜態檔案 |
| `src/WebApp/Services/SupabaseService.cs` | Supabase REST：查 listings、CRUD favorites |
| `src/WebApp/Models/Listing.cs` | Listing domain model（同 Plan 1，但獨立維護） |
| `src/WebApp/Models/Favorite.cs` | Favorite model |
| `src/WebApp/wwwroot/index.html` | 主頁面，Google Maps 容器 + 右側面板 HTML |
| `src/WebApp/wwwroot/app.js` | 地圖初始化、marker 渲染、API 呼叫、右側面板邏輯 |
| `src/WebApp/wwwroot/style.css` | 版面樣式 |
| `Dockerfile` | Railway 部署用 |
| `tests/WebApp.Tests/WebApp.Tests.csproj` | 測試專案 |
| `tests/WebApp.Tests/Services/SupabaseServiceTests.cs` | SupabaseService 單元測試 |

---

## Task 1: WebApp 專案設定

**Files:**
- Create: `src/WebApp/WebApp.csproj`
- Create: `tests/WebApp.Tests/WebApp.Tests.csproj`

- [ ] **Step 1: 建立 ASP.NET Core Web App 專案**

```bash
dotnet new web -n WebApp -o src/WebApp --framework net8.0
dotnet sln add src/WebApp/WebApp.csproj
```

- [ ] **Step 2: 建立測試專案**

```bash
dotnet new xunit -n WebApp.Tests -o tests/WebApp.Tests --framework net8.0
dotnet sln add tests/WebApp.Tests/WebApp.Tests.csproj
dotnet add tests/WebApp.Tests/WebApp.Tests.csproj reference src/WebApp/WebApp.csproj
```

- [ ] **Step 3: 新增 NuGet 套件**

```bash
dotnet add src/WebApp/WebApp.csproj package Microsoft.Extensions.Http
```

- [ ] **Step 4: 確認 build**

```bash
dotnet build src/WebApp/WebApp.csproj
```

預期：`Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/WebApp/ tests/WebApp.Tests/
git commit -m "chore: add WebApp and WebApp.Tests projects"
```

---

## Task 2: Models（Listing + Favorite）

**Files:**
- Create: `src/WebApp/Models/Listing.cs`
- Create: `src/WebApp/Models/Favorite.cs`

- [ ] **Step 1: 建立 Listing.cs**

建立 `src/WebApp/Models/Listing.cs`：

```csharp
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

    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();

    [JsonPropertyName("scraped_at")]
    public DateTimeOffset ScrapedAt { get; set; }
}
```

- [ ] **Step 2: 建立 Favorite.cs**

建立 `src/WebApp/Models/Favorite.cs`：

```csharp
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

    // Joined from listings (populated in GetFavoritesWithListings)
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
```

- [ ] **Step 3: Commit**

```bash
git add src/WebApp/Models/
git commit -m "feat: add WebApp Listing and Favorite models"
```

---

## Task 3: WebApp SupabaseService

**Files:**
- Create: `src/WebApp/Services/SupabaseService.cs`
- Create: `tests/WebApp.Tests/Services/SupabaseServiceTests.cs`
- Create: `tests/WebApp.Tests/Helpers/MockHttpMessageHandler.cs`

- [ ] **Step 1: 複製 MockHttpMessageHandler 到 WebApp.Tests**

建立 `tests/WebApp.Tests/Helpers/MockHttpMessageHandler.cs`：

```csharp
using System.Net;
using System.Text;

namespace WebApp.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UrlContains, HttpStatusCode Status, string Body)> _rules = new();

    public void Setup(string urlContains, HttpStatusCode status, string body)
        => _rules.Add((urlContains, status, body));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        foreach (var (urlContains, status, body) in _rules)
        {
            if (url.Contains(urlContains))
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
    }
}
```

- [ ] **Step 2: 寫 failing tests**

建立 `tests/WebApp.Tests/Services/SupabaseServiceTests.cs`：

```csharp
using System.Net;
using WebApp.Models;
using WebApp.Services;
using WebApp.Tests.Helpers;
using Xunit;

namespace WebApp.Tests.Services;

public class SupabaseServiceTests
{
    private static SupabaseService Build(MockHttpMessageHandler handler)
        => new(new HttpClient(handler), "https://fake.supabase.co", "fake-key");

    [Fact]
    public async Task GetListings_ReturnsDeserializedListings()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/listings", HttpStatusCode.OK, """
            [
              {"id":"1","title":"套房A","price":15000,"address":"大安區","lat":25.033,
               "lng":121.565,"size_ping":10,"room_type":"套房","has_furniture":true,
               "has_natural_gas":false,"has_cable_tv":false,"has_internet":true,
               "has_parking":false,"pet_allowed":false,"url":"https://x.com","images":[],
               "scraped_at":"2026-05-14T00:00:00Z"}
            ]
            """);

        var svc = Build(handler);
        var listings = await svc.GetListingsAsync();

        Assert.Single(listings);
        Assert.Equal("套房A", listings[0].Title);
        Assert.True(listings[0].HasInternet);
    }

    [Fact]
    public async Task GetFavoritesWithListings_JoinsListingData()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.OK, """
            [
              {"listing_id":"1","note":"","status":"待看","favorited_at":"2026-05-14T00:00:00Z",
               "listing":{"id":"1","title":"套房A","price":15000,"address":"大安區",
                "lat":25.033,"lng":121.565,"size_ping":10,"room_type":"套房",
                "has_furniture":true,"has_natural_gas":false,"has_cable_tv":false,
                "has_internet":true,"has_parking":false,"pet_allowed":false,
                "url":"https://x.com","images":[],"scraped_at":"2026-05-14T00:00:00Z"}}
            ]
            """);

        var svc = Build(handler);
        var favs = await svc.GetFavoritesWithListingsAsync();

        Assert.Single(favs);
        Assert.Equal("套房A", favs[0].Listing?.Title);
        Assert.Equal("待看", favs[0].Status);
    }

    [Fact]
    public async Task AddFavorite_PostsToSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.Created, "");

        var svc = Build(handler);
        await svc.AddFavoriteAsync("listing-id-1");
        // No exception = success
    }

    [Fact]
    public async Task RemoveFavorite_DeletesFromSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.NoContent, "");

        var svc = Build(handler);
        await svc.RemoveFavoriteAsync("listing-id-1");
        // No exception = success
    }

    [Fact]
    public async Task UpdateFavorite_PatchesSupabase()
    {
        var handler = new MockHttpMessageHandler();
        handler.Setup("/rest/v1/favorites", HttpStatusCode.NoContent, "");

        var svc = Build(handler);
        await svc.UpdateFavoriteAsync("listing-id-1", new FavoriteUpdateRequest
        {
            Status = "已看",
            Note = "下週去看"
        });
        // No exception = success
    }
}
```

- [ ] **Step 3: 執行，確認 fail**

```bash
dotnet test tests/WebApp.Tests/ --filter "SupabaseServiceTests"
```

預期：fail（SupabaseService 不存在）

- [ ] **Step 4: 實作 SupabaseService**

建立 `src/WebApp/Services/SupabaseService.cs`：

```csharp
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
        // Supabase embedded resource: select=*,listing:listings(*)
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
```

- [ ] **Step 5: 執行，確認 pass**

```bash
dotnet test tests/WebApp.Tests/ --filter "SupabaseServiceTests"
```

預期：`Passed! - Failed: 0, Passed: 5`

- [ ] **Step 6: Commit**

```bash
git add src/WebApp/Services/ tests/WebApp.Tests/
git commit -m "feat: add WebApp SupabaseService with tests"
```

---

## Task 4: Program.cs — API 路由

**Files:**
- Modify: `src/WebApp/Program.cs`

- [ ] **Step 1: 實作 Program.cs（Minimal API）**

覆寫 `src/WebApp/Program.cs`：

```csharp
using WebApp.Models;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new InvalidOperationException("SUPABASE_URL 未設定");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new InvalidOperationException("SUPABASE_KEY 未設定");

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SupabaseService(factory.CreateClient(), supabaseUrl, supabaseKey);
});

var app = builder.Build();
app.UseStaticFiles();

// ── Listings ────────────────────────────────────────────────────
app.MapGet("/api/listings", async (
    SupabaseService svc,
    bool? hasFurniture,
    bool? hasInternet,
    bool? hasNaturalGas,
    bool? hasParking,
    bool? petAllowed,
    int? maxPrice) =>
    Results.Ok(await svc.GetListingsAsync(
        hasFurniture, hasInternet, hasNaturalGas, hasParking, petAllowed, maxPrice)));

// ── Favorites ───────────────────────────────────────────────────
app.MapGet("/api/favorites",
    async (SupabaseService svc) =>
        Results.Ok(await svc.GetFavoritesWithListingsAsync()));

app.MapPost("/api/favorites/{id}",
    async (string id, SupabaseService svc) =>
    {
        await svc.AddFavoriteAsync(id);
        return Results.Created($"/api/favorites/{id}", null);
    });

app.MapDelete("/api/favorites/{id}",
    async (string id, SupabaseService svc) =>
    {
        await svc.RemoveFavoriteAsync(id);
        return Results.NoContent();
    });

app.MapPatch("/api/favorites/{id}",
    async (string id, FavoriteUpdateRequest req, SupabaseService svc) =>
    {
        await svc.UpdateFavoriteAsync(id, req);
        return Results.NoContent();
    });

// Fallback: serve index.html for SPA
app.MapFallbackToFile("index.html");

app.Run();
```

- [ ] **Step 2: 本機啟動確認 API 正常**

先設定環境變數（PowerShell）：

```powershell
$env:SUPABASE_URL = "https://xxxx.supabase.co"
$env:SUPABASE_KEY = "your-anon-key"
```

啟動：

```bash
dotnet run --project src/WebApp
```

測試 API：

```bash
curl http://localhost:5000/api/listings
```

預期：回傳 Supabase 的 listings JSON 陣列。

- [ ] **Step 3: Commit**

```bash
git add src/WebApp/Program.cs
git commit -m "feat: add Minimal API routes for listings and favorites"
```

---

## Task 5: 前端 HTML + CSS

**Files:**
- Create: `src/WebApp/wwwroot/index.html`
- Create: `src/WebApp/wwwroot/style.css`

- [ ] **Step 1: 建立 wwwroot 目錄**

```bash
mkdir -p src/WebApp/wwwroot
```

- [ ] **Step 2: 建立 index.html**

建立 `src/WebApp/wwwroot/index.html`：

```html
<!DOCTYPE html>
<html lang="zh-TW">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>591 租屋追蹤</title>
  <link rel="stylesheet" href="/style.css">
</head>
<body>
  <nav class="topnav">
    <span class="logo">591 租屋追蹤</span>
    <div class="nav-tabs">
      <button class="tab active" data-view="favorites" onclick="switchView('favorites')">⭐ 收藏</button>
      <button class="tab" data-view="all" onclick="switchView('all')">🔍 所有物件</button>
    </div>
    <span class="count-badge" id="countBadge">載入中...</span>
  </nav>

  <div class="layout">
    <div id="map"></div>

    <aside class="sidebar">
      <!-- Filter panel -->
      <div class="panel" id="filterPanel">
        <h4>篩選條件</h4>
        <label><input type="checkbox" id="filterFurniture"> 有家具</label>
        <label><input type="checkbox" id="filterInternet"> 有網路</label>
        <label><input type="checkbox" id="filterGas"> 天然氣</label>
        <label><input type="checkbox" id="filterParking"> 停車位</label>
        <label><input type="checkbox" id="filterPet"> 可養寵物</label>
        <div class="price-filter">
          <label>租金上限
            <input type="number" id="filterMaxPrice" placeholder="不限" step="1000">
          </label>
        </div>
        <button class="btn-filter" onclick="applyFilters()">套用篩選</button>
      </div>

      <!-- Listing list -->
      <div class="panel" id="listPanel">
        <h4 id="listTitle">收藏物件</h4>
        <div id="listContainer"></div>
      </div>
    </aside>
  </div>

  <!-- Info window modal (shown when marker clicked) -->
  <div id="infoModal" class="modal hidden">
    <div class="modal-content">
      <button class="modal-close" onclick="closeModal()">✕</button>
      <img id="modalPhoto" src="" alt="物件照片" class="modal-photo hidden">
      <div class="modal-body">
        <h3 id="modalTitle"></h3>
        <div class="modal-price" id="modalPrice"></div>
        <div class="modal-address" id="modalAddress"></div>
        <div class="modal-meta" id="modalMeta"></div>
        <div class="modal-amenities" id="modalAmenities"></div>
        <div class="modal-status-row" id="modalStatusRow"></div>
        <div class="modal-note-row" id="modalNoteRow"></div>
        <div class="modal-actions" id="modalActions"></div>
        <a id="modalLink" href="#" target="_blank" class="btn-591">🔗 查看 591 原始頁</a>
      </div>
    </div>
  </div>

  <script>
    // Google Maps API key 從後端取得（避免直接寫死在 HTML）
    fetch('/api/maps-key')
      .then(r => r.json())
      .then(data => {
        const script = document.createElement('script');
        script.src = `https://maps.googleapis.com/maps/api/js?key=${data.key}&callback=initMap&language=zh-TW`;
        script.async = true;
        document.head.appendChild(script);
      });
  </script>
  <script src="/app.js"></script>
</body>
</html>
```

- [ ] **Step 3: 新增 `/api/maps-key` 端點（避免 key 直接寫在 HTML）**

編輯 `src/WebApp/Program.cs`，在其他路由後加入：

```csharp
var googleMapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
    ?? throw new InvalidOperationException("GOOGLE_MAPS_API_KEY 未設定");

// 在 app.MapGet("/api/listings", ...) 之前加入
app.MapGet("/api/maps-key", () => Results.Ok(new { key = googleMapsKey }));
```

也在 `builder` 階段讀取這個 key：

```csharp
var googleMapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
    ?? throw new InvalidOperationException("GOOGLE_MAPS_API_KEY 未設定");
```

- [ ] **Step 4: 建立 style.css**

建立 `src/WebApp/wwwroot/style.css`：

```css
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  background: #0f172a; color: #e2e8f0; height: 100vh; display: flex; flex-direction: column; }

.topnav { display: flex; align-items: center; gap: 16px; padding: 10px 20px;
  background: #1e293b; border-bottom: 1px solid #334155; height: 52px; flex-shrink: 0; }
.logo { font-weight: 700; color: #6366f1; font-size: 1.1em; }
.nav-tabs { display: flex; gap: 4px; }
.tab { background: transparent; border: 1px solid #334155; color: #94a3b8;
  padding: 5px 14px; border-radius: 6px; cursor: pointer; font-size: 0.85em; }
.tab.active { background: #6366f1; border-color: #6366f1; color: white; }
.count-badge { margin-left: auto; font-size: 0.83em; color: #64748b; }

.layout { display: flex; flex: 1; overflow: hidden; }
#map { flex: 1; }

.sidebar { width: 280px; background: #1e293b; border-left: 1px solid #334155;
  display: flex; flex-direction: column; overflow-y: auto; }

.panel { padding: 14px; border-bottom: 1px solid #334155; }
.panel h4 { font-size: 0.82em; text-transform: uppercase; letter-spacing: 0.05em;
  color: #64748b; margin-bottom: 10px; }

.panel label { display: flex; align-items: center; gap: 8px; font-size: 0.88em;
  color: #94a3b8; margin-bottom: 6px; cursor: pointer; }
.panel input[type="checkbox"] { accent-color: #6366f1; }
.price-filter { margin-top: 8px; }
.price-filter label { flex-direction: column; align-items: flex-start; gap: 4px; }
.price-filter input { width: 100%; background: #0f172a; border: 1px solid #334155;
  border-radius: 4px; padding: 5px 8px; color: #e2e8f0; font-size: 0.88em; }
.btn-filter { width: 100%; margin-top: 10px; padding: 7px; background: #6366f1;
  border: none; border-radius: 6px; color: white; cursor: pointer; font-size: 0.88em; }
.btn-filter:hover { background: #4f46e5; }

.listing-card { background: #0f172a; border-radius: 6px; padding: 10px 12px;
  margin-bottom: 6px; cursor: pointer; border-left: 3px solid transparent;
  transition: border-color 0.15s; }
.listing-card:hover { border-left-color: #6366f1; }
.listing-card.active { border-left-color: #10b981; }
.card-title { font-size: 0.88em; font-weight: 600; margin-bottom: 3px; white-space: nowrap;
  overflow: hidden; text-overflow: ellipsis; }
.card-price { color: #10b981; font-size: 0.85em; font-weight: 600; }
.card-status { font-size: 0.75em; color: #64748b; margin-top: 2px; }

/* Modal */
.modal { position: fixed; inset: 0; background: rgba(0,0,0,0.6);
  display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal.hidden { display: none; }
.modal-content { background: #1e293b; border-radius: 12px; max-width: 420px; width: 90%;
  max-height: 85vh; overflow-y: auto; position: relative; }
.modal-close { position: absolute; top: 10px; right: 14px; background: transparent;
  border: none; color: #94a3b8; font-size: 1.2em; cursor: pointer; z-index: 1; }
.modal-photo { width: 100%; height: 200px; object-fit: cover;
  border-radius: 12px 12px 0 0; }
.modal-photo.hidden { display: none; }
.modal-body { padding: 16px; }
.modal-body h3 { font-size: 1em; font-weight: 700; margin-bottom: 8px; }
.modal-price { color: #10b981; font-weight: 700; font-size: 1.1em; margin-bottom: 4px; }
.modal-address { color: #94a3b8; font-size: 0.85em; margin-bottom: 4px; }
.modal-meta { color: #64748b; font-size: 0.82em; margin-bottom: 10px; }
.modal-amenities { font-size: 0.85em; line-height: 2; margin-bottom: 10px; }
.modal-status-row, .modal-note-row { margin-bottom: 8px; }
.modal-status-row select, .modal-note-row textarea {
  width: 100%; background: #0f172a; border: 1px solid #334155; border-radius: 4px;
  padding: 6px 8px; color: #e2e8f0; font-size: 0.85em; }
.modal-note-row textarea { resize: vertical; min-height: 60px; }
.modal-actions { display: flex; gap: 8px; margin-bottom: 10px; }
.btn-favorite { flex: 1; padding: 7px; border-radius: 6px; border: none;
  cursor: pointer; font-size: 0.85em; font-weight: 600; }
.btn-add-fav { background: #10b981; color: white; }
.btn-remove-fav { background: #ef4444; color: white; }
.btn-save { background: #6366f1; color: white; flex: 1; padding: 7px;
  border-radius: 6px; border: none; cursor: pointer; font-size: 0.85em; }
.btn-591 { display: block; text-align: center; padding: 8px; background: #0f172a;
  border: 1px solid #334155; border-radius: 6px; color: #6ab3f0; text-decoration: none;
  font-size: 0.85em; }
```

- [ ] **Step 5: Commit**

```bash
git add src/WebApp/wwwroot/ src/WebApp/Program.cs
git commit -m "feat: add frontend HTML and CSS"
```

---

## Task 6: 前端 JavaScript（app.js）

**Files:**
- Create: `src/WebApp/wwwroot/app.js`

- [ ] **Step 1: 建立 app.js**

建立 `src/WebApp/wwwroot/app.js`：

```javascript
let map;
let markers = [];
let currentListings = [];
let currentFavorites = [];
let favoriteIds = new Set();
let activeView = 'favorites';
let currentListingId = null;

// ── Map init (called by Google Maps API callback) ────────────────
window.initMap = function () {
  map = new google.maps.Map(document.getElementById('map'), {
    center: { lat: 25.033, lng: 121.565 }, // Taipei
    zoom: 13,
    styles: darkMapStyles()
  });
  loadView('favorites');
};

// ── View switching ───────────────────────────────────────────────
window.switchView = function (view) {
  activeView = view;
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.view === view));
  loadView(view);
};

async function loadView(view) {
  clearMarkers();
  if (view === 'favorites') {
    await loadFavorites();
  } else {
    await loadAllListings();
  }
}

// ── Favorites view ───────────────────────────────────────────────
async function loadFavorites() {
  const favs = await apiFetch('/api/favorites');
  currentFavorites = favs;
  favoriteIds = new Set(favs.map(f => f.listing_id));

  const listings = favs.map(f => f.listing).filter(Boolean);
  document.getElementById('countBadge').textContent = `收藏 ${listings.length} 筆`;
  document.getElementById('listTitle').textContent = '收藏物件';
  renderList(listings, favs);
  renderMarkers(listings);
}

// ── All listings view ────────────────────────────────────────────
async function loadAllListings() {
  const listings = await apiFetch('/api/listings');
  currentListings = listings;
  document.getElementById('countBadge').textContent = `${listings.length} 筆物件`;
  document.getElementById('listTitle').textContent = '所有物件';
  renderList(listings, currentFavorites);
  renderMarkers(listings);
}

// ── Filter ───────────────────────────────────────────────────────
window.applyFilters = async function () {
  const params = new URLSearchParams();
  if (document.getElementById('filterFurniture').checked) params.set('hasFurniture', 'true');
  if (document.getElementById('filterInternet').checked) params.set('hasInternet', 'true');
  if (document.getElementById('filterGas').checked) params.set('hasNaturalGas', 'true');
  if (document.getElementById('filterParking').checked) params.set('hasParking', 'true');
  if (document.getElementById('filterPet').checked) params.set('petAllowed', 'true');
  const maxPrice = document.getElementById('filterMaxPrice').value;
  if (maxPrice) params.set('maxPrice', maxPrice);

  const listings = await apiFetch('/api/listings?' + params.toString());
  currentListings = listings;
  clearMarkers();
  renderList(listings, currentFavorites);
  renderMarkers(listings);
};

// ── Render markers ───────────────────────────────────────────────
function renderMarkers(listings) {
  listings.forEach(listing => {
    if (!listing.lat || !listing.lng) return;

    const isFav = favoriteIds.has(listing.id);
    const marker = new google.maps.Marker({
      position: { lat: listing.lat, lng: listing.lng },
      map,
      title: listing.title,
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 9,
        fillColor: isFav ? '#10b981' : '#6366f1',
        fillOpacity: 1,
        strokeColor: '#fff',
        strokeWeight: 2
      }
    });

    marker.addListener('click', () => openModal(listing));
    markers.push(marker);
  });
}

function clearMarkers() {
  markers.forEach(m => m.setMap(null));
  markers = [];
}

// ── Render sidebar list ──────────────────────────────────────────
function renderList(listings, favs) {
  const favMap = Object.fromEntries(favs.map(f => [f.listing_id, f]));
  const container = document.getElementById('listContainer');
  container.innerHTML = '';

  if (listings.length === 0) {
    container.innerHTML = '<p style="color:#64748b;font-size:0.85em;text-align:center;padding:16px">無物件</p>';
    return;
  }

  listings.forEach(listing => {
    const fav = favMap[listing.id];
    const card = document.createElement('div');
    card.className = 'listing-card' + (fav ? ' active' : '');
    card.innerHTML = `
      <div class="card-title" title="${listing.title}">${listing.title}</div>
      <div class="card-price">$${listing.price.toLocaleString()} / 月</div>
      <div class="card-status">${fav ? fav.status : listing.room_type} · ${listing.size_ping} 坪</div>
    `;
    card.addEventListener('click', () => {
      openModal(listing, fav);
      if (listing.lat && listing.lng)
        map.panTo({ lat: listing.lat, lng: listing.lng });
    });
    container.appendChild(card);
  });
}

// ── Info modal ───────────────────────────────────────────────────
function openModal(listing, fav) {
  currentListingId = listing.id;
  const isFav = favoriteIds.has(listing.id);

  const photo = listing.images?.[0];
  const photoEl = document.getElementById('modalPhoto');
  if (photo) { photoEl.src = photo; photoEl.classList.remove('hidden'); }
  else { photoEl.classList.add('hidden'); }

  document.getElementById('modalTitle').textContent = listing.title;
  document.getElementById('modalPrice').textContent = `$${listing.price.toLocaleString()} / 月`;
  document.getElementById('modalAddress').textContent = listing.address;
  document.getElementById('modalMeta').textContent = `${listing.size_ping} 坪 · ${listing.room_type}`;
  document.getElementById('modalLink').href = listing.url;

  const b = v => v ? '✅' : '❌';
  document.getElementById('modalAmenities').innerHTML = `
    🪑 家具 ${b(listing.has_furniture)} &nbsp; 🔥 天然氣 ${b(listing.has_natural_gas)}<br>
    📺 第四台 ${b(listing.has_cable_tv)} &nbsp; 🌐 網路 ${b(listing.has_internet)}<br>
    🚗 停車 ${b(listing.has_parking)} &nbsp; 🐾 寵物 ${b(listing.pet_allowed)}
  `;

  // Status & note (only when favorited)
  const statusRow = document.getElementById('modalStatusRow');
  const noteRow = document.getElementById('modalNoteRow');
  if (isFav && fav) {
    statusRow.innerHTML = `<select id="modalStatus" onchange="saveStatus(this.value)">
      ${['待看','已看','已洽談','不考慮'].map(s =>
        `<option value="${s}" ${fav.status === s ? 'selected' : ''}>${s}</option>`
      ).join('')}
    </select>`;
    noteRow.innerHTML = `<textarea id="modalNote" placeholder="備註..." onblur="saveNote(this.value)">${fav.note || ''}</textarea>`;
  } else {
    statusRow.innerHTML = '';
    noteRow.innerHTML = '';
  }

  document.getElementById('modalActions').innerHTML = isFav
    ? `<button class="btn-favorite btn-remove-fav" onclick="removeFavorite()">移除收藏</button>`
    : `<button class="btn-favorite btn-add-fav" onclick="addFavorite()">加入收藏</button>`;

  document.getElementById('infoModal').classList.remove('hidden');
}

window.closeModal = function () {
  document.getElementById('infoModal').classList.add('hidden');
  currentListingId = null;
};

// ── Favorite actions ─────────────────────────────────────────────
window.addFavorite = async function () {
  await apiFetch(`/api/favorites/${currentListingId}`, 'POST');
  await loadView(activeView);
  closeModal();
};

window.removeFavorite = async function () {
  await apiFetch(`/api/favorites/${currentListingId}`, 'DELETE');
  await loadView(activeView);
  closeModal();
};

window.saveStatus = async function (status) {
  await apiFetch(`/api/favorites/${currentListingId}`, 'PATCH', { status });
};

window.saveNote = async function (note) {
  await apiFetch(`/api/favorites/${currentListingId}`, 'PATCH', { note });
};

// ── API helper ───────────────────────────────────────────────────
async function apiFetch(url, method = 'GET', body = null) {
  const options = {
    method,
    headers: { 'Content-Type': 'application/json' }
  };
  if (body) options.body = JSON.stringify(body);
  const res = await fetch(url, options);
  if (method === 'GET') return res.json();
  return res;
}

// ── Dark map style ───────────────────────────────────────────────
function darkMapStyles() {
  return [
    { elementType: 'geometry', stylers: [{ color: '#1d2c4d' }] },
    { elementType: 'labels.text.fill', stylers: [{ color: '#8ec3b9' }] },
    { elementType: 'labels.text.stroke', stylers: [{ color: '#1a3646' }] },
    { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#304a7d' }] },
    { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0e1626' }] }
  ];
}
```

- [ ] **Step 2: 本機完整測試**

```powershell
$env:SUPABASE_URL = "https://xxxx.supabase.co"
$env:SUPABASE_KEY = "your-anon-key"
$env:GOOGLE_MAPS_API_KEY = "your-maps-key"
```

```bash
dotnet run --project src/WebApp
```

打開 `http://localhost:5000`，確認：
- 地圖正常顯示（台北中心）
- 收藏物件顯示 marker
- 點擊 marker 彈出 modal，顯示照片、設備、狀態
- 加入 / 移除收藏正常
- 切換到「所有物件」顯示全部 listings

- [ ] **Step 3: Commit**

```bash
git add src/WebApp/wwwroot/app.js
git commit -m "feat: add Google Maps frontend with marker, modal, favorites"
```

---

## Task 7: Railway 部署

**Files:**
- Create: `Dockerfile`

- [ ] **Step 1: 建立 Dockerfile**

建立 `Dockerfile`（根目錄）：

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/WebApp/WebApp.csproj", "src/WebApp/"]
RUN dotnet restore "src/WebApp/WebApp.csproj"
COPY . .
RUN dotnet publish "src/WebApp/WebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebApp.dll"]
```

- [ ] **Step 2: 本機確認 Docker build（選用）**

```bash
docker build -t rental-webapp .
docker run -p 8080:8080 \
  -e SUPABASE_URL=... -e SUPABASE_KEY=... -e GOOGLE_MAPS_API_KEY=... \
  rental-webapp
```

打開 `http://localhost:8080` 確認正常。

- [ ] **Step 3: 部署至 Railway**

1. 到 [railway.app](https://railway.app)，建立新 Project → Deploy from GitHub repo
2. 選擇 `rental-monitor` repo
3. Railway 會自動偵測 Dockerfile
4. 到 Variables 分頁，加入：
   - `SUPABASE_URL`
   - `SUPABASE_KEY`
   - `GOOGLE_MAPS_API_KEY`
5. 等待部署完成，取得 Railway 提供的網域（例如 `rental-webapp.up.railway.app`）
6. 到 Google Cloud Console → Maps JavaScript API → 限制 HTTP Referrer 為該網域

- [ ] **Step 4: Commit**

```bash
git add Dockerfile
git commit -m "feat: add Dockerfile for Railway deployment"
git push origin main
```

---

## 完成檢查

```bash
dotnet test --verbosity normal
```

預期：所有測試綠燈。

打開 Railway 提供的網址，確認地圖、收藏、篩選功能全部正常。
