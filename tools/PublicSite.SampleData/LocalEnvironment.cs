internal static class LocalEnvironment
{
    public static Dictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return values;
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator > 0) values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return values;
    }

    public static void Write(string path, IReadOnlyDictionary<string, string> values)
    {
        File.WriteAllLines(path, values.Select(FormatEntry));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string FormatEntry(KeyValuePair<string, string> entry) => entry.Key + "=" + entry.Value;
}
