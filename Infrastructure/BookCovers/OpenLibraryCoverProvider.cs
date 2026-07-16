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
        foreach (var title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using var response = await _httpClient.GetAsync(
                $"/search.json?title={Uri.EscapeDataString(title)}&limit=5&fields=title,cover_i", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("docs", out var docs) ||
                docs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in docs.EnumerateArray())
            {
                if (item.TryGetProperty("cover_i", out var coverId) && coverId.TryGetInt32(out var id))
                {
                    return new BookCoverCandidate(BookCoverSource.OpenLibrary,
                        $"https://covers.openlibrary.org/b/id/{id}-L.jpg");
                }
            }
        }

        return null;
    }
}
