namespace Scraper.Config;

public class ScraperConfig
{
    public int MaxPrice { get; set; } = 20000;
    public double MinSizePing { get; set; } = 8;
    public List<string> Districts { get; set; } = new();
    public List<string> RoomTypes { get; set; } = new();
    public bool RequireFurniture { get; set; } = false;
    public bool RequireInternet { get; set; } = false;
    public int NotifyHour { get; set; } = 8;
}
