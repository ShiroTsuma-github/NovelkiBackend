namespace Infrastructure.BookCovers;

using System.Text.Json;

internal static class BookCoverJson
{
    public static JsonElement? TryGetProperty(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current;
    }

    public static string? TryGetString(JsonElement element, params string[] path)
    {
        var property = TryGetProperty(element, path);
        return property?.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
    }
}
