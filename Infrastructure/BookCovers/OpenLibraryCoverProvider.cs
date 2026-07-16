namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class OpenLibraryCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public OpenLibraryCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (string title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                $"/search.json?title={Uri.EscapeDataString(title)}&limit=5&fields=title,cover_i", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("docs", out JsonElement docs) ||
                docs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement item in docs.EnumerateArray())
            {
                if (item.TryGetProperty("cover_i", out JsonElement coverId) && coverId.TryGetInt32(out int id))
                {
                    return new BookCoverCandidate(BookCoverSource.OpenLibrary,
                        $"https://covers.openlibrary.org/b/id/{id}-L.jpg");
                }
            }
        }

        return null;
    }
}
