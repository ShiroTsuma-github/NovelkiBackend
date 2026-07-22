namespace Infrastructure.BookMetadata;

using AngleSharp.Dom;
using static BookHtmlResolverUtilities;

internal sealed class RoyalRoadHtmlResolver : IBookHtmlResolver
{
    private const string SourceName = "RoyalRoad";
    private const string Host = "royalroad.com";

    public int Match(IDocument document)
    {
        var canonical = ReadCanonicalUrl(document);
        var hasCanonical = HasHostAndPath(canonical, Host, "/fiction/");
        var hasTitle = document.QuerySelector(".fic-header h1") != null;
        var markerCount = new[] { ".tags a.fiction-tag", "img[data-type='cover']", ".description" }
            .Count(selector => document.QuerySelector(selector) != null);
        var score = (hasCanonical ? 60 : 0) + (hasTitle ? 20 : 0) + markerCount * 10;
        return (hasCanonical && (hasTitle || markerCount > 0)) || (hasTitle && markerCount > 0) || markerCount >= 2
            ? score
            : 0;
    }

    public ResolvedBookHtmlMetadata Resolve(IDocument document)
    {
        var warnings = new List<string>();
        var canonical = ValidateCanonical(document, warnings);
        var title = ReadText(document.QuerySelector(".fic-header h1"))
                    ?? ReadBookJsonLdString(document, "name")
                    ?? ReadMeta(document, "og:title");
        if (title == null)
        {
            warnings.Add("Primary title was not found in the pasted Royal Road HTML.");
        }

        var author = ReadText(document.QuerySelector(".fic-header h4 a"))
                     ?? ReadBookJsonLdAuthor(document);
        var description = ReadMultilineText(document.QuerySelector(".description"));
        if (description == null)
        {
            description = HtmlToMultilineText(ReadBookJsonLdString(document, "description"), document);
        }

        var candidates = ReadDistinctTexts(document.QuerySelectorAll(".tags a.fiction-tag"));
        var tags = ReadDistinctTexts(document.QuerySelectorAll(".font-red-sunglo ul.list-inline li"));
        var cover = ReadCover(document, canonical, warnings);

        return new ResolvedBookHtmlMetadata(
            SourceName, title, author, "Novel", Array.Empty<string>(), Array.Empty<string>(), tags, candidates,
            description, canonical, cover, warnings);
    }

    private static string? ValidateCanonical(IDocument document, ICollection<string> warnings)
    {
        var raw = ReadCanonicalUrl(document);
        var canonical = NormalizeHttpUrl(raw);
        if (HasHostAndPath(canonical, Host, "/fiction/"))
        {
            return canonical;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            warnings.Add("The canonical Royal Road fiction URL was invalid and was skipped.");
        }

        return null;
    }

    private static string? ReadCover(IDocument document, string? canonical, ICollection<string> warnings)
    {
        var image = document.QuerySelector("img.thumbnail[data-type='cover'], img[data-type='cover']");
        var raw = image?.GetAttribute("src") ?? image?.GetAttribute("data-src")
            ?? ReadBookJsonLdString(document, "image") ?? ReadMeta(document, "og:image");
        var cover = NormalizeHttpUrl(raw, canonical);
        if (!string.IsNullOrWhiteSpace(raw) && cover == null)
        {
            warnings.Add("The detected Royal Road cover URL was invalid and was skipped.");
        }

        return cover;
    }
}
