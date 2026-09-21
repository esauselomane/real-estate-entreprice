namespace Property.Microservice.Services.BackgroundJobs;

internal static class SheetValueParser
{
    public static string Text(Dictionary<string, string> row, string column, string defaultValue = "")
    {
        var value = row.GetValueOrDefault(column);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Trim();
    }

    public static string? NullableText(Dictionary<string, string> row, string column)
    {
        var value = row.GetValueOrDefault(column);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    public static int Int(Dictionary<string, string> row, string column, int defaultValue = 0)
    {
        var value = row.GetValueOrDefault(column);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public static int? NullableInt(Dictionary<string, string> row, string column)
    {
        var value = row.GetValueOrDefault(column);
        return int.TryParse(value, out var result) ? result : null;
    }

    public static decimal Decimal(Dictionary<string, string> row, string column, decimal defaultValue = 0m)
    {
        var value = row.GetValueOrDefault(column);
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }

    public static decimal? NullableDecimal(Dictionary<string, string> row, string column)
    {
        var value = row.GetValueOrDefault(column);
        return decimal.TryParse(value, out var result) ? result : null;
    }

    public static bool Bool(Dictionary<string, string> row, string column, bool defaultValue = false)
    {
        var value = row.GetValueOrDefault(column);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public static DateTime? DateTimeValue(Dictionary<string, string> row, string column)
    {
        var value = row.GetValueOrDefault(column);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(value, out var result) ? result : null;
    }
}
