namespace Infrastructure.BookMetadata;

using AngleSharp.Dom;
using static BookHtmlResolverUtilities;

internal sealed class WebNovelHtmlResolver : IBookHtmlResolver
{
    private const string SourceName = "WebNovel";
    private const string Host = "webnovel.com";

    public int Match(IDocument document)
    {
        var canonical = ReadCanonicalUrl(document);
        var hasCanonical = HasHostAndPath(canonical, Host, "/book/");
        var hasTitle = document.QuerySelector(".det-hd h1") != null;
        var markerCount = new[] { ".j_synopsis", ".j_tagWrap .m-tag", ".det-hd a.c_primary" }
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
        var title = ReadText(document.QuerySelector(".det-hd h1"))
                    ?? ReadMeta(document, "og:title")
                    ?? ReadBookJsonLdString(document, "name");
        if (title == null)
        {
            warnings.Add("Primary title was not found in the pasted WebNovel HTML.");
        }

        var author = ReadText(document.QuerySelector(".det-hd a.c_primary[title]"))
                     ?? ReadMeta(document, "og:author")
                     ?? ReadBookJsonLdAuthor(document);
        var description = ReadMultilineText(document.QuerySelector(".j_synopsis"))
                          ?? ReadMeta(document, "og:description");

        var candidates = ReadDistinctTexts(document.QuerySelectorAll(".det-hd-tag, .j_tagWrap .m-tag a"))
            .Select(RemoveTagPrefix)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cover = ReadCover(document, canonical, warnings);

        return new ResolvedBookHtmlMetadata(
            SourceName, title, author, "Novel", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            candidates,
            description, canonical, cover, warnings);
    }

    private static string RemoveTagPrefix(string value)
    {
        return value.Trim().TrimStart('#').Trim();
    }

    private static string? ValidateCanonical(IDocument document, ICollection<string> warnings)
    {
        var raw = ReadCanonicalUrl(document);
        var canonical = NormalizeHttpUrl(raw);
        if (HasHostAndPath(canonical, Host, "/book/"))
        {
            return canonical;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            warnings.Add("The canonical WebNovel book URL was invalid and was skipped.");
        }

        return null;
    }

    private static string? ReadCover(IDocument document, string? canonical, ICollection<string> warnings)
    {
        var image = document.QuerySelector(".det-hd img");
        var raw = ReadMeta(document, "og:image") ?? image?.GetAttribute("src") ?? image?.GetAttribute("data-src");
        var cover = NormalizeHttpUrl(raw, canonical);
        if (!string.IsNullOrWhiteSpace(raw) && cover == null)
        {
            warnings.Add("The detected WebNovel cover URL was invalid and was skipped.");
        }

        return cover;
    }
}
