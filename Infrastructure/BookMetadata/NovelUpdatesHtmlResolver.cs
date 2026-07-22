namespace Infrastructure.BookMetadata;

using System.Text.RegularExpressions;
using AngleSharp.Dom;

internal sealed partial class NovelUpdatesHtmlResolver : IBookHtmlResolver
{
    private const string SourceName = "NovelUpdates";
    private const string NovelUpdatesHost = "novelupdates.com";

    public int Match(IDocument document)
    {
        var score = 0;
        var canonical = ReadCanonicalUrl(document);
        var hasSeriesUrl = IsNovelUpdatesSeriesUrl(canonical);
        if (hasSeriesUrl)
        {
            score += 60;
        }

        var hasSeriesTitle = document.QuerySelector(".seriestitlenu") != null;
        if (hasSeriesTitle)
        {
            score += 20;
        }

        var contentMarkers = 0;
        foreach (var selector in new[] { "#seriesgenre", "#showauthors", "#editdescription", "div.seriesimg img" })
        {
            if (document.QuerySelector(selector) != null)
            {
                score += 10;
                contentMarkers++;
            }
        }

        return (hasSeriesUrl && hasSeriesTitle) || (hasSeriesTitle && contentMarkers > 0) || contentMarkers >= 2
            ? score
            : 0;
    }

    public ResolvedBookHtmlMetadata Resolve(IDocument document)
    {
        var warnings = new List<string>();
        var rawCanonicalUrl = ReadCanonicalUrl(document);
        var canonicalUrl = IsNovelUpdatesSeriesUrl(rawCanonicalUrl) ? NormalizeHttpUrl(rawCanonicalUrl) : null;
        if (!string.IsNullOrWhiteSpace(rawCanonicalUrl) && canonicalUrl == null)
        {
            warnings.Add("The canonical NovelUpdates series URL was invalid and was skipped.");
        }

        var primaryTitle = ReadText(document.QuerySelector(".seriestitlenu")) ?? ReadText(document.QuerySelector("h1"));
        if (primaryTitle == null)
        {
            warnings.Add("Primary title was not found in the pasted HTML.");
        }

        var authorName = document.QuerySelectorAll("#showauthors a.genre")
            .Select(ReadText)
            .FirstOrDefault(value => value != null);
        var genres = ReadDistinctTexts(document.QuerySelectorAll("#seriesgenre a.genre"));
        var tags = ReadDistinctTexts(document.QuerySelectorAll("#showtags a.genre"))
            .Concat(ReadDistinctTexts(document.QuerySelectorAll("#showlang a.genre.lang")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var alternativeTitles = ReadLines(document.QuerySelector("#editassociated"))
            .Where(title => !string.Equals(title, primaryTitle, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var description = ReadMultilineText(document.QuerySelector("#editdescription"));
        var coverUrl = ReadCoverUrl(document, canonicalUrl, warnings);

        return new ResolvedBookHtmlMetadata(
            SourceName,
            primaryTitle,
            authorName,
            "Novel",
            alternativeTitles,
            genres,
            tags,
            Array.Empty<string>(),
            description,
            canonicalUrl,
            coverUrl,
            warnings);
    }

    private static string? ReadCanonicalUrl(IDocument document)
    {
        return document.QuerySelector("link[rel~='canonical']")?.GetAttribute("href")
               ?? document.QuerySelector("meta[property='og:url']")?.GetAttribute("content");
    }

    private static bool IsNovelUpdatesSeriesUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Host.Equals(NovelUpdatesHost, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith($".{NovelUpdatesHost}", StringComparison.OrdinalIgnoreCase)) &&
               uri.AbsolutePath.StartsWith("/series/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadCoverUrl(IDocument document, string? canonicalUrl, ICollection<string> warnings)
    {
        var image = document.QuerySelector("div.seriesimg img");
        if (image == null)
        {
            return null;
        }

        var raw = image.GetAttribute("src")
                  ?? image.GetAttribute("data-src")
                  ?? image.GetAttribute("data-lazy-src")
                  ?? image.GetAttribute("data-original")
                  ?? FirstSrcSetUrl(image.GetAttribute("srcset"));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var absolute))
        {
            return IsHttp(absolute) ? absolute.ToString() : null;
        }

        if (Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, raw.Trim(), out var resolved) && IsHttp(resolved))
        {
            return resolved.ToString();
        }

        warnings.Add("The detected cover URL was invalid and was skipped.");
        return null;
    }

    private static string? FirstSrcSetUrl(string? srcSet)
    {
        return srcSet?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static IReadOnlyCollection<string> ReadDistinctTexts(IEnumerable<IElement> elements)
    {
        return elements.Select(ReadText)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ReadLines(IElement? element)
    {
        if (element == null)
        {
            return Array.Empty<string>();
        }

        var htmlWithLines = BreakElementRegex().Replace(element.InnerHtml, "\n");
        var container = element.Owner?.CreateElement("div");
        if (container == null)
        {
            return Array.Empty<string>();
        }

        container.InnerHtml = htmlWithLines;
        return container.TextContent
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeInlineText)
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static string? ReadText(IElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var value = NormalizeInlineText(element.TextContent);
        return value.Length == 0 ? null : value;
    }

    private static string? ReadMultilineText(IElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var value = LineWhitespaceRegex().Replace(element.TextContent.Replace("\r\n", "\n"), "\n").Trim();
        return value.Length == 0 ? null : value;
    }

    private static string NormalizeInlineText(string value)
    {
        return InlineWhitespaceRegex().Replace(value, " ").Trim();
    }

    private static string? NormalizeHttpUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && IsHttp(uri) &&
               uri.ToString().Length <= 2000
            ? uri.ToString()
            : null;
    }

    private static bool IsHttp(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakElementRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"\n\s*\n+", RegexOptions.CultureInvariant)]
    private static partial Regex LineWhitespaceRegex();
}
