namespace Application.Common;

public static class BookSearchSuggestionFields
{
    public const string Author = "author";
    public const string Tag = "tag";
    public const string Genre = "genre";
    public const string Status = "status";
    public const string Type = "type";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Author,
        Tag,
        Genre,
        Status,
        Type
    };
}
