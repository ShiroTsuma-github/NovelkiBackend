namespace Domain.Repositories;

public sealed record BookSearchCriteria(
    IReadOnlyCollection<string> Terms,
    IReadOnlyCollection<BookSearchFieldFilter> Fields,
    IReadOnlyCollection<BookSearchNumberFilter> Numbers,
    IReadOnlyCollection<BookSearchDateFilter> Dates,
    IReadOnlyCollection<BookSearchMissingFilter> Missing)
{
    public BookSearchCriteria(
        IReadOnlyCollection<string> terms,
        IReadOnlyCollection<BookSearchFieldFilter> fields,
        IReadOnlyCollection<BookSearchNumberFilter> numbers)
        : this(terms, fields, numbers, Array.Empty<BookSearchDateFilter>(), Array.Empty<BookSearchMissingFilter>())
    {
    }

    public static BookSearchCriteria Empty { get; } =
        new(
            Array.Empty<string>(),
            Array.Empty<BookSearchFieldFilter>(),
            Array.Empty<BookSearchNumberFilter>(),
            Array.Empty<BookSearchDateFilter>(),
            Array.Empty<BookSearchMissingFilter>());

    public bool HasFilters => Terms.Count > 0 || Fields.Count > 0 || Numbers.Count > 0 || Dates.Count > 0 || Missing.Count > 0;
}

public sealed record BookSearchFieldFilter(BookSearchField Field, IReadOnlyCollection<string> Values)
{
    public BookSearchFieldFilter(BookSearchField field, string value)
        : this(field, new[] { value })
    {
    }
}

public sealed record BookSearchNumberFilter(BookSearchNumberField Field, BookSearchOperator Operator, decimal Value);

public sealed record BookSearchDateFilter(BookSearchDateField Field, BookSearchOperator Operator, DateOnly Value);

public sealed record BookSearchMissingFilter(BookSearchMissingField Field);

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

public enum BookSearchDateField
{
    Created,
    LastModified
}

public enum BookSearchMissingField
{
    Rating,
    Priority,
    Author,
    Genre,
    Tag,
    CurrentChapter,
    TotalChapters,
    Cover,
    Link
}

public enum BookSearchOperator
{
    Equal,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}
