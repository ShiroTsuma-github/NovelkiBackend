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
        foreach (string title in BookCoverProviderHelpers.EnumerateTitles(book))
        {
            var payload = new { query = Query, variables = new { search = title } };
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement? media = BookCoverJson.TryGetProperty(document.RootElement, "data", "Page", "media");
            if (media == null || media.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement item in media.Value.EnumerateArray())
            {
                string? imageUrl = BookCoverJson.TryGetString(item, "coverImage", "extraLarge")
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
