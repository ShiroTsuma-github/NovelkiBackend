namespace Infrastructure.BookCovers;

using System.Net.Http.Json;
using System.Text.Json;

public sealed class AniListBookCoverProvider : IBookCoverProvider
{
    private const string Query = """
        query ($search: String) {
          Page(page: 1, perPage: 5) {
            media(search: $search, type: MANGA) {
              coverImage {
                extraLarge
                large
              }
            }
          }
        }
        """;

    private readonly HttpClient _httpClient;

    public AniListBookCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            var payload = new { query = Query, variables = new { search = title } };
            using var response = await _httpClient.PostAsJsonAsync("", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var media = BookCoverJson.TryGetProperty(document.RootElement, "data", "Page", "media");
            if (media == null || media.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in media.Value.EnumerateArray())
            {
                var imageUrl = BookCoverJson.TryGetString(item, "coverImage", "extraLarge")
                    ?? BookCoverJson.TryGetString(item, "coverImage", "large");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.AniList, imageUrl);
                }
            }
        }

        return null;
    }

}
