namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class GoogleBooksCoverProvider : IBookCoverProvider
{
    private const string VolumeInfoProperty = "volumeInfo";
    private const string ImageLinksProperty = "imageLinks";

    private readonly HttpClient _httpClient;

    public GoogleBooksCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using var response = await _httpClient.GetAsync(
                $"/books/v1/volumes?q={Uri.EscapeDataString($"intitle:{title}")}&maxResults=5&projection=lite",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var imageUrl = BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty, "extraLarge")
                               ?? BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty, "large")
                               ?? BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty, "medium")
                               ?? BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty, "small")
                               ?? BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty, "thumbnail")
                               ?? BookCoverJson.TryGetString(item, VolumeInfoProperty, ImageLinksProperty,
                                   "smallThumbnail");
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
