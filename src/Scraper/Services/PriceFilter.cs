using Scraper.Config;

namespace Scraper.Services;

public static class PriceFilter
{
    public static bool IsWithinRange(string priceText, ScraperConfig config)
    {
        if (!int.TryParse(priceText, out var price) || price <= 0)
            return true;

        return price >= config.MinPrice && price <= config.MaxPrice;
    }
}
