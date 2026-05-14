namespace Scraper.Config;

public class ScraperConfig
{
    public int Region { get; set; } = 1;
    public int MinPrice { get; set; } = 0;
    public int MaxPrice { get; set; } = 20000;
    public double MinSizePing { get; set; } = 8;
    public List<string> Districts { get; set; } = new();
    public List<int> SectionCodes { get; set; } = new();
    public List<string> RoomTypes { get; set; } = new();
    public bool RequireFurniture { get; set; } = false;
    public bool RequireInternet { get; set; } = false;
    public int NotifyHour { get; set; } = 8;
}
