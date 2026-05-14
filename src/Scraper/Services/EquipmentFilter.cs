using Scraper.Config;
using Scraper.Models;

namespace Scraper.Services;

public static class EquipmentFilter
{
    public static bool ShouldInclude(ScraperConfig config, Listing listing, bool detailFetched)
    {
        if (config.RequireFurniture && detailFetched && !listing.HasFurniture)
            return false;

        if (config.RequireInternet && detailFetched && !listing.HasInternet)
            return false;

        return true;
    }
}
