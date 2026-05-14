using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class EnvFileLoaderTests
{
    [Fact]
    public void ParseLines_LoadsKeyValuePairs()
    {
        var lines = new[]
        {
            "SUPABASE_URL=https://example.supabase.co",
            "SUPABASE_KEY=abc123",
            "TELEGRAM_CHAT_ID=123456"
        };

        var values = EnvFileLoader.ParseLines(lines);

        Assert.Equal("https://example.supabase.co", values["SUPABASE_URL"]);
        Assert.Equal("abc123", values["SUPABASE_KEY"]);
        Assert.Equal("123456", values["TELEGRAM_CHAT_ID"]);
    }

    [Fact]
    public void ParseLines_IgnoresCommentsAndBlankLines()
    {
        var lines = new[]
        {
            "",
            "   ",
            "# comment",
            "GOOGLE_MAPS_API_KEY=test-key"
        };

        var values = EnvFileLoader.ParseLines(lines);

        Assert.Single(values);
        Assert.Equal("test-key", values["GOOGLE_MAPS_API_KEY"]);
    }

    [Fact]
    public void ParseLines_RemovesWrappingQuotes()
    {
        var lines = new[]
        {
            "TELEGRAM_BOT_TOKEN=\"quoted-value\""
        };

        var values = EnvFileLoader.ParseLines(lines);

        Assert.Equal("quoted-value", values["TELEGRAM_BOT_TOKEN"]);
    }
}
