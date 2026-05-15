using WebApp.Models;
using WebApp.Services;

EnvFileLoader.LoadIfExists(
    Path.Combine(AppContext.BaseDirectory, ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "src/WebApp/.env"),
    Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ── Listings ────────────────────────────────────────────────────
app.MapGet("/api/listings", async (
    SupabaseService svc,
    bool? hasFurniture,
    bool? hasInternet,
    bool? hasNaturalGas,
    bool? hasParking,
    bool? petAllowed,
    int? maxPrice,
    double? minSizePing,
    double? maxSizePing) =>
    Results.Ok(await svc.GetListingsAsync(
        hasFurniture, hasInternet, hasNaturalGas, hasParking, petAllowed,
        maxPrice, minSizePing, maxSizePing)));

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

app.MapFallbackToFile("index.html");

app.Run();
