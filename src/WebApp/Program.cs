using WebApp.Models;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new InvalidOperationException("SUPABASE_URL 未設定");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new InvalidOperationException("SUPABASE_KEY 未設定");
var googleMapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
    ?? throw new InvalidOperationException("GOOGLE_MAPS_API_KEY 未設定");

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SupabaseService(factory.CreateClient(), supabaseUrl, supabaseKey);
});

var app = builder.Build();
app.UseStaticFiles();

// ── Maps key (avoids hardcoding key in HTML) ────────────────────
app.MapGet("/api/maps-key", () => Results.Ok(new { key = googleMapsKey }));

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

app.MapFallbackToFile("index.html");

app.Run();
