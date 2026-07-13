namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class WikidataCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public WikidataCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            var query = "SELECT ?image WHERE {\n" +
                        $"  ?item rdfs:label \"{EscapeSparqlString(title)}\"@en.\n" +
                        "  ?item wdt:P18 ?image.\n" +
                        "}\n" +
                        "LIMIT 1";
            using var response = await _httpClient.GetAsync($"/sparql?format=json&query={Uri.EscapeDataString(query)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var bindings = TryGetProperty(document.RootElement, "results", "bindings");
            if (bindings == null || bindings.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var binding in bindings.Value.EnumerateArray())
            {
                var imageUrl = TryGetString(binding, "image", "value");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.Wikidata, imageUrl);
                }
            }
        }

        return null;
    }

    private static string EscapeSparqlString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static JsonElement? TryGetProperty(JsonElement element, params string[] path)
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

    private static string? TryGetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
