namespace Application.Common.DTOs.Book;

public sealed record BookHtmlDictionaryMatch(Guid? Id, string Name);

public sealed record BookHtmlParseResult(
    string Source,
    string? PrimaryTitle,
    string? AuthorName,
    BookHtmlDictionaryMatch? ContentType,
    IReadOnlyCollection<string> AlternativeTitles,
    IReadOnlyCollection<BookHtmlDictionaryMatch> Genres,
    IReadOnlyCollection<string> Tags,
    string? Description,
    string? CanonicalUrl,
    string? CoverUrl,
    IReadOnlyCollection<string> Warnings);
