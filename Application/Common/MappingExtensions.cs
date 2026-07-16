namespace Application.Common;

public static partial class MappingExtensions
{
    public static string NormalizeName(string value)
    {
        return CollapseWhitespace(value).ToUpperInvariant();
    }

    public static string CollapseWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeSlug(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}
