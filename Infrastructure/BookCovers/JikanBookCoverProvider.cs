namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class JikanBookCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public JikanBookCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (string title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync($"/v4/manga?q={Uri.EscapeDataString(title)}&limit=5", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement item in data.EnumerateArray())
            {
                string? imageUrl = BookCoverJson.TryGetString(item, "images", "jpg", "large_image_url")
                                   ?? BookCoverJson.TryGetString(item, "images", "jpg", "image_url");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.Jikan, imageUrl);
                }
            }
        }

        return null;
    }
}
