namespace WebApp.Services;

public static class EnvFileLoader
{
    public static void LoadIfExists(params string[] candidatePaths)
    {
        foreach (var path in candidatePaths.Distinct())
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            foreach (var pair in ParseLines(File.ReadAllLines(path)))
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(pair.Key)))
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            return;
        }
    }

    private static Dictionary<string, string> ParseLines(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var sep = line.IndexOf('=');
            if (sep <= 0) continue;
            var key = line[..sep].Trim();
            var val = line[(sep + 1)..].Trim();
            if (val.Length >= 2 &&
                ((val.StartsWith('"') && val.EndsWith('"')) ||
                 (val.StartsWith('\'') && val.EndsWith('\''))))
                val = val[1..^1];
            result[key] = val;
        }
        return result;
    }
}
