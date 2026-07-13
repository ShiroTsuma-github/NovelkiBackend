namespace Infrastructure.BookCovers;

using System.Net;
using System.Text.RegularExpressions;

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
