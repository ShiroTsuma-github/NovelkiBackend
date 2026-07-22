namespace Infrastructure.BookMetadata;

using AngleSharp.Dom;

internal sealed record ResolvedBookHtmlMetadata(
    string Source,
    string? PrimaryTitle,
    string? AuthorName,
    string? ContentTypeName,
    IReadOnlyCollection<string> AlternativeTitles,
    IReadOnlyCollection<string> Genres,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> GenreOrTagCandidates,
    string? Description,
    string? CanonicalUrl,
    string? CoverUrl,
    IReadOnlyCollection<string> Warnings);

internal interface IBookHtmlResolver
{
    public int Match(IDocument document);

    public ResolvedBookHtmlMetadata Resolve(IDocument document);
}
