namespace Infrastructure.BookCovers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed record BookCoverCandidate(BookCoverSource Source, string ImageUrl);

public interface IBookCoverProvider
{
    Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken);
}

public sealed partial class BookLinkMetadataCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public BookLinkMetadataCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var link in book.Links.OrderByDescending(l => l.IsPrimary))
        {
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var pageUri) ||
                pageUri.Scheme is not ("http" or "https"))
            {
                continue;
            }

            using var response = await _httpClient.GetAsync(pageUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (html.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("cf_chl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var imageUrl in ExtractImageUrls(html, pageUri))
            {
                return new BookCoverCandidate(BookCoverSource.BookLinkMetadata, imageUrl);
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractImageUrls(string html, Uri pageUri)
    {
        foreach (Match match in MetaRegex().Matches(html))
        {
            var key = WebUtility.HtmlDecode(match.Groups["key"].Value).Trim().ToLowerInvariant();
            if (key is not ("og:image" or "og:image:url" or "twitter:image" or "twitter:image:src"))
            {
                continue;
            }

            var content = WebUtility.HtmlDecode(match.Groups["content"].Value).Trim();
            if (TryNormalizeImageUrl(content, pageUri, out var imageUrl))
            {
                yield return imageUrl;
            }
        }

        foreach (Match match in LinkImageRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (TryNormalizeImageUrl(href, pageUri, out var imageUrl))
            {
                yield return imageUrl;
            }
        }
    }

    private static bool TryNormalizeImageUrl(string value, Uri pageUri, out string imageUrl)
    {
        imageUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("logo", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("spacer", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("avatar", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uri = Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(pageUri, value);
        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        imageUrl = uri.ToString();
        return true;
    }

    [GeneratedRegex("<meta[^>]+(?:property|name)=[\"'](?<key>og:image|og:image:url|twitter:image|twitter:image:src)[\"'][^>]+content=[\"'](?<content>[^\"']+)[\"'][^>]*|<meta[^>]+content=[\"'](?<content>[^\"']+)[\"'][^>]+(?:property|name)=[\"'](?<key>og:image|og:image:url|twitter:image|twitter:image:src)[\"'][^>]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaRegex();

    [GeneratedRegex("<link[^>]+rel=[\"'][^\"']*(?:image_src|preload)[^\"']*[\"'][^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LinkImageRegex();
}

public sealed class JikanBookCoverProvider : IBookCoverProvider
{
    private readonly HttpClient _httpClient;

    public JikanBookCoverProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var title in EnumerateTitles(book))
        {
            using var response = await _httpClient.GetAsync($"/v4/manga?q={Uri.EscapeDataString(title)}&limit=5", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in data.EnumerateArray())
            {
                var imageUrl = TryGetString(item, "images", "jpg", "large_image_url")
                    ?? TryGetString(item, "images", "jpg", "image_url");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.Jikan, imageUrl);
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateTitles(Book book) => BookCoverProviderHelpers.EnumerateTitles(book);

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
        foreach (var title in EnumerateTitles(book))
        {
            var payload = new { query = Query, variables = new { search = title } };
            using var response = await _httpClient.PostAsJsonAsync("", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var media = TryGetProperty(document.RootElement, "data", "Page", "media");
            if (media == null || media.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in media.Value.EnumerateArray())
            {
                var imageUrl = TryGetString(item, "coverImage", "extraLarge")
                    ?? TryGetString(item, "coverImage", "large");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return new BookCoverCandidate(BookCoverSource.AniList, imageUrl);
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateTitles(Book book) => BookCoverProviderHelpers.EnumerateTitles(book);

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
            using var response = await _httpClient.GetAsync($"/search.json?title={Uri.EscapeDataString(title)}&limit=5&fields=title,cover_i", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in docs.EnumerateArray())
            {
                if (item.TryGetProperty("cover_i", out var coverId) && coverId.TryGetInt32(out var id))
                {
                    return new BookCoverCandidate(BookCoverSource.OpenLibrary, $"https://covers.openlibrary.org/b/id/{id}-L.jpg");
                }
            }
        }

        return null;
    }
}

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

    private static string EscapeSparqlString(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

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

public sealed class BookCoverResolver
{
    private readonly IEnumerable<IBookCoverProvider> _providers;

    public BookCoverResolver(IEnumerable<IBookCoverProvider> providers)
    {
        _providers = providers;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var candidate = await provider.FindAsync(book, cancellationToken);
                if (candidate != null)
                {
                    return candidate;
                }
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                continue;
            }
            catch (JsonException)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        return null;
    }
}

internal static class BookCoverProviderHelpers
{
    public static IEnumerable<string> EnumerateTitles(Book book)
    {
        yield return book.PrimaryTitle;

        foreach (var title in book.Titles.Where(t => !t.IsPrimary).Select(t => t.Title))
        {
            yield return title;
        }
    }
}
