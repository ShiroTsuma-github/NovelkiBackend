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
        foreach (var title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using var response = await _httpClient.GetAsync($"/books/v1/volumes?q={Uri.EscapeDataString($"intitle:{title}")}&maxResults=5&projection=lite", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var imageUrl = TryGetString(item, "volumeInfo", "imageLinks", "extraLarge")
                    ?? TryGetString(item, "volumeInfo", "imageLinks", "large")
                    ?? TryGetString(item, "volumeInfo", "imageLinks", "medium")
                    ?? TryGetString(item, "volumeInfo", "imageLinks", "small")
                    ?? TryGetString(item, "volumeInfo", "imageLinks", "thumbnail")
                    ?? TryGetString(item, "volumeInfo", "imageLinks", "smallThumbnail");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.GoogleBooks, imageUrl.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        return null;
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
