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
        foreach (string title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            string query = "SELECT ?image WHERE {\n" +
                           $"  ?item rdfs:label \"{EscapeSparqlString(title)}\"@en.\n" +
                           "  ?item wdt:P18 ?image.\n" +
                           "}\n" +
                           "LIMIT 1";
            using HttpResponseMessage response =
                await _httpClient.GetAsync($"/sparql?format=json&query={Uri.EscapeDataString(query)}",
                    cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement? bindings = BookCoverJson.TryGetProperty(document.RootElement, "results", "bindings");
            if (bindings == null || bindings.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement binding in bindings.Value.EnumerateArray())
            {
                string? imageUrl = BookCoverJson.TryGetString(binding, "image", "value");
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
}
