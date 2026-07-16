namespace Domain.Models;

public static class BookAnalyticsQualityFields
{
    public const string Author = "author";
    public const string Description = "description";
    public const string Genre = "genre";
    public const string Tag = "tag";
    public const string Rating = "rating";
    public const string Priority = "priority";
    public const string TotalChapters = "totalChapters";
    public const string Link = "link";
    public const string AlternateTitle = "alternateTitle";
    public const string UsableCover = "usableCover";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(
    [
        Author,
        Description,
        Genre,
        Tag,
        Rating,
        Priority,
        TotalChapters,
        Link,
        AlternateTitle,
        UsableCover
    ]);
}
