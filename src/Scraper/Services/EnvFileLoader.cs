namespace Scraper.Services;

public static class EnvFileLoader
{
    public static void LoadIfExists(params string[] candidatePaths)
    {
        foreach (var path in candidatePaths.Distinct())
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var values = ParseLines(File.ReadAllLines(path));
            foreach (var pair in values)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(pair.Key)))
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            return;
        }
    }

    internal static Dictionary<string, string> ParseLines(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }
}
