namespace Domain.Repositories;

public sealed record BookSearchCriteria(
    IReadOnlyCollection<string> Terms,
    IReadOnlyCollection<BookSearchFieldFilter> Fields,
    IReadOnlyCollection<BookSearchNumberFilter> Numbers)
{
    public static BookSearchCriteria Empty { get; } =
        new(Array.Empty<string>(), Array.Empty<BookSearchFieldFilter>(), Array.Empty<BookSearchNumberFilter>());

    public bool HasFilters => Terms.Count > 0 || Fields.Count > 0 || Numbers.Count > 0;
}

public sealed record BookSearchFieldFilter(BookSearchField Field, string Value);

public sealed record BookSearchNumberFilter(BookSearchNumberField Field, BookSearchOperator Operator, decimal Value);

public enum BookSearchField
{
    Title,
    Author,
    Tag,
    Genre,
    Status,
    Type
}

public enum BookSearchNumberField
{
    Rating,
    Priority,
    CurrentChapter,
    TotalChapters
}

public enum BookSearchOperator
{
    Equal,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}
