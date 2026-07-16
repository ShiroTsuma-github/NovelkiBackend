namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class GoogleBooksCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public GoogleBooksCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (string title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                $"/books/v1/volumes?q={Uri.EscapeDataString($"intitle:{title}")}&maxResults=5&projection=lite",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("items", out JsonElement items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                string? imageUrl = BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "extraLarge")
                                   ?? BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "large")
                                   ?? BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "medium")
                                   ?? BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "small")
                                   ?? BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "thumbnail")
                                   ?? BookCoverJson.TryGetString(item, "volumeInfo", "imageLinks", "smallThumbnail");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.GoogleBooks,
                        imageUrl.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        return null;
    }
}
