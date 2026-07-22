namespace Infrastructure.BookMetadata;

using AngleSharp.Dom;
using static BookHtmlResolverUtilities;

internal sealed class ScribbleHubHtmlResolver : IBookHtmlResolver
{
    private const string SourceName = "ScribbleHub";
    private const string Host = "scribblehub.com";

    public int Match(IDocument document)
    {
        var canonical = ReadCanonicalUrl(document);
        var hasCanonical = HasHostAndPath(canonical, Host, "/series/");
        var hasTitle = document.QuerySelector(".fic_title") != null;
        var markerCount = new[] { ".wi_fic_desc", ".fic_genre", ".fic_image img" }
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
        var title = ReadText(document.QuerySelector(".fic_title"))
                    ?? ReadBookJsonLdString(document, "name")
                    ?? ReadMeta(document, "og:title");
        if (title == null)
        {
            warnings.Add("Primary title was not found in the pasted Scribble Hub HTML.");
        }

        var author = ReadText(document.QuerySelector(".auth_name_fic"))
                     ?? ReadBookJsonLdAuthor(document);
        var description = ReadMultilineText(document.QuerySelector(".wi_fic_desc"));
        if (description == null)
        {
            description = HtmlToMultilineText(ReadBookJsonLdString(document, "description"), document);
        }

        var genres = ReadDistinctTexts(document.QuerySelectorAll(".fic_genre"));
        var tags = ReadDistinctTexts(document.QuerySelectorAll(".wi_fic_showtags a.stag, .mature_contains a"));
        var cover = ReadCover(document, canonical, warnings);

        return new ResolvedBookHtmlMetadata(
            SourceName, title, author, "Novel", Array.Empty<string>(), genres, tags, Array.Empty<string>(),
            description, canonical, cover, warnings);
    }

    private static string? ValidateCanonical(IDocument document, ICollection<string> warnings)
    {
        var raw = ReadCanonicalUrl(document);
        var canonical = NormalizeHttpUrl(raw);
        if (HasHostAndPath(canonical, Host, "/series/"))
        {
            return canonical;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            warnings.Add("The canonical Scribble Hub series URL was invalid and was skipped.");
        }

        return null;
    }

    private static string? ReadCover(IDocument document, string? canonical, ICollection<string> warnings)
    {
        var image = document.QuerySelector(".fic_image img[property='image'], .fic_image img");
        var raw = image?.GetAttribute("src") ?? image?.GetAttribute("data-src")
            ?? ReadBookJsonLdString(document, "image") ?? ReadMeta(document, "og:image");
        var cover = NormalizeHttpUrl(raw, canonical);
        if (!string.IsNullOrWhiteSpace(raw) && cover == null)
        {
            warnings.Add("The detected Scribble Hub cover URL was invalid and was skipped.");
        }

        return cover;
    }
}
