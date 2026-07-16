namespace Domain.Models;

public static class BookAnalyticsBuckets
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Default = Week;

    public static bool IsSupported(string? value)
    {
        return string.Equals(value, Day, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Week, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Month, StringComparison.OrdinalIgnoreCase);
    }
}
