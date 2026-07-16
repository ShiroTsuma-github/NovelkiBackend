namespace Infrastructure.Persistence;

using Domain.Entities;
using System.Globalization;
using System.Linq.Expressions;

public sealed class BookSearchCriteriaApplier
{
    private readonly bool _supportsILike;
    private readonly TextSearchExpressionFactory _textSearch;

    public BookSearchCriteriaApplier(ApplicationDbContext context)
    {
        _supportsILike = context.Database.IsNpgsql();
        _textSearch = new TextSearchExpressionFactory(_supportsILike);
    }

    public IQueryable<Book> Apply(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        query = criteria.Terms.Aggregate(query, ApplyGeneralTextSearch);

        query = criteria.Fields.Aggregate(query, (current, filter) => filter.Field switch
        {
            BookSearchField.Title => ApplyTitleSearch(current, filter.Values),
            BookSearchField.Author => ApplyAuthorSearch(current, filter.Values),
            BookSearchField.Tag => ApplyTagSearch(current, filter.Values),
            BookSearchField.Genre => ApplyGenreSearch(current, filter.Values),
            BookSearchField.Status => ApplyStatusSearch(current, filter.Values),
            BookSearchField.Type => ApplyTypeSearch(current, filter.Values),
            _ => current
        });

        query = criteria.Numbers.Aggregate(query, (current, filter) => filter.Field switch
        {
            BookSearchNumberField.Rating => ApplyRating(current, filter.Operator, filter.Value),
            BookSearchNumberField.Priority => ApplyPriority(current, filter.Operator, filter.Value),
            BookSearchNumberField.CurrentChapter => ApplyCurrentChapter(current, filter.Operator, filter.Value),
            BookSearchNumberField.TotalChapters => ApplyTotalChapters(current, filter.Operator, filter.Value),
            _ => current
        });

        query = criteria.Dates.Aggregate(query, (current, filter) => filter.Field switch
        {
            BookSearchDateField.Created => ApplyCreatedDate(current, filter.Operator, filter.Value),
            BookSearchDateField.LastModified => ApplyLastModifiedDate(current, filter.Operator, filter.Value),
            _ => current
        });

        return criteria.Missing.Aggregate(query, (current, filter) => filter.Field switch
        {
            BookSearchMissingField.Rating => current.Where(book => book.Rating == null),
            BookSearchMissingField.Priority => current.Where(book => book.Priority == null),
            BookSearchMissingField.Author => current.Where(book => book.Author == null),
            BookSearchMissingField.Genre => current.Where(book => !book.BookGenres.Any()),
            BookSearchMissingField.Tag => current.Where(book => !book.BookTags.Any()),
            BookSearchMissingField.CurrentChapter => current.Where(book => book.CurrentChapterNumber == null),
            BookSearchMissingField.TotalChapters => current.Where(book => book.TotalChapters == null),
            BookSearchMissingField.Cover => current.Where(book => book.Cover == null),
            BookSearchMissingField.Link => current.Where(book => !book.Links.Any()),
            _ => current
        });
    }

    private IQueryable<Book> ApplyGeneralTextSearch(IQueryable<Book> query, string term)
    {
        Expression<Func<Book, bool>> authorMatch = PredicateExpression.And<Book>(
            book => book.Author != null,
            PredicateExpression.Or(
                _textSearch.Match<Book>(
                    book => book.Author!.PrimaryName,
                    book => book.Author!.NormalizedPrimaryName,
                    term),
                _textSearch.AnyMatch<Book, AuthorName>(
                    book => book.Author!.Names,
                    name => name.Name,
                    name => name.NormalizedName,
                    term)));

        Expression<Func<Book, bool>>? predicate = PredicateExpression.OrAll(
        [
            _textSearch.Match<Book>(
                book => book.PrimaryTitle,
                book => book.NormalizedPrimaryTitle,
                term),
            _textSearch.AnyMatch<Book, BookTitle>(
                book => book.Titles,
                title => title.Title,
                title => title.NormalizedTitle,
                term),
            authorMatch
        ])!;

        return query.Where(predicate);
    }

    private IQueryable<Book> ApplyTitleSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search => PredicateExpression.Or(
            _textSearch.Match<Book>(
                book => book.PrimaryTitle,
                book => book.NormalizedPrimaryTitle,
                search),
            _textSearch.AnyMatch<Book, BookTitle>(
                book => book.Titles,
                title => title.Title,
                title => title.NormalizedTitle,
                search)));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyAuthorSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search => PredicateExpression.And<Book>(
            book => book.Author != null,
            PredicateExpression.Or(
                _textSearch.Match<Book>(
                    book => book.Author!.PrimaryName,
                    book => book.Author!.NormalizedPrimaryName,
                    search),
                _textSearch.AnyMatch<Book, AuthorName>(
                    book => book.Author!.Names,
                    name => name.Name,
                    name => name.NormalizedName,
                    search))));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTagSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search =>
            _textSearch.AnyMatch<Book, BookTag>(
                book => book.BookTags,
                bookTag => bookTag.Tag.Name,
                bookTag => bookTag.Tag.NormalizedName,
                search));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyGenreSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search =>
            _textSearch.AnyMatch<Book, BookGenre>(
                book => book.BookGenres,
                bookGenre => bookGenre.Genre.Name,
                bookGenre => bookGenre.Genre.NormalizedName,
                search));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyStatusSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search =>
            _textSearch.Match<Book>(
                book => book.Status.Name,
                book => book.Status.Name.ToUpper(),
                search));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTypeSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        IEnumerable<Expression<Func<Book, bool>>> predicates = searches.Select(search =>
            _textSearch.Match<Book>(
                book => book.ContentType.Name,
                book => book.ContentType.Name.ToUpper(),
                search));

        return ApplyAnyFieldMatch(query, predicates);
    }

    private static IQueryable<Book> ApplyAnyFieldMatch(
        IQueryable<Book> query,
        IEnumerable<Expression<Func<Book, bool>>> predicates)
    {
        Expression<Func<Book, bool>>? combined = PredicateExpression.OrAll(predicates);
        return combined == null ? query : query.Where(combined);
    }

    private IQueryable<Book> ApplyRating(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        if (!_supportsILike)
        {
            double sqliteValue = (double)value;
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book =>
                    book.Rating != null && book.Rating.Value > sqliteValue),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                    book.Rating != null && book.Rating.Value >= sqliteValue),
                BookSearchOperator.LessThan => query.Where(book =>
                    book.Rating != null && book.Rating.Value < sqliteValue),
                BookSearchOperator.LessThanOrEqual => query.Where(book =>
                    book.Rating != null && book.Rating.Value <= sqliteValue),
                _ => query.Where(book => book.Rating != null && book.Rating.Value == sqliteValue)
            };
        }

        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                book.Rating != null && (decimal)book.Rating.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                book.Rating != null && (decimal)book.Rating.Value >= value),
            BookSearchOperator.LessThan =>
                query.Where(book => book.Rating != null && (decimal)book.Rating.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                book.Rating != null && (decimal)book.Rating.Value <= value),
            _ => query.Where(book => book.Rating != null && (decimal)book.Rating.Value == value)
        };
    }

    private IQueryable<Book> ApplyPriority(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        if (!_supportsILike)
        {
            double sqliteValue = (double)value;
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book =>
                    book.Priority != null && book.Priority.Value > sqliteValue),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                    book.Priority != null && book.Priority.Value >= sqliteValue),
                BookSearchOperator.LessThan => query.Where(book =>
                    book.Priority != null && book.Priority.Value < sqliteValue),
                BookSearchOperator.LessThanOrEqual => query.Where(book =>
                    book.Priority != null && book.Priority.Value <= sqliteValue),
                _ => query.Where(book => book.Priority != null && book.Priority.Value == sqliteValue)
            };
        }

        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                book.Priority != null && (decimal)book.Priority.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                book.Priority != null && (decimal)book.Priority.Value >= value),
            BookSearchOperator.LessThan => query.Where(book =>
                book.Priority != null && (decimal)book.Priority.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                book.Priority != null && (decimal)book.Priority.Value <= value),
            _ => query.Where(book => book.Priority != null && (decimal)book.Priority.Value == value)
        };
    }

    private static IQueryable<Book> ApplyCurrentChapter(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                book.CurrentChapterNumber != null && book.CurrentChapterNumber > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                book.CurrentChapterNumber != null && book.CurrentChapterNumber >= value),
            BookSearchOperator.LessThan => query.Where(book =>
                book.CurrentChapterNumber != null && book.CurrentChapterNumber < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                book.CurrentChapterNumber != null && book.CurrentChapterNumber <= value),
            _ => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber == value)
        };
    }

    private static IQueryable<Book> ApplyTotalChapters(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                book.TotalChapters != null && book.TotalChapters > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                book.TotalChapters != null && book.TotalChapters >= value),
            BookSearchOperator.LessThan =>
                query.Where(book => book.TotalChapters != null && book.TotalChapters < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                book.TotalChapters != null && book.TotalChapters <= value),
            _ => query.Where(book => book.TotalChapters != null && book.TotalChapters == value)
        };
    }

    private IQueryable<Book> ApplyCreatedDate(IQueryable<Book> query, BookSearchOperator op, DateOnly value)
    {
        (DateTimeOffset start, DateTimeOffset nextDay) = ToUtcDateBounds(value);
        if (_supportsILike)
        {
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book => book.Created >= nextDay),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Created >= start),
                BookSearchOperator.LessThan => query.Where(book => book.Created < start),
                BookSearchOperator.LessThanOrEqual => query.Where(book => book.Created < nextDay),
                _ => query.Where(book => book.Created >= start && book.Created < nextDay)
            };
        }

        string startText = ToSqliteDateTimeOffsetString(start);
        string nextDayText = ToSqliteDateTimeOffsetString(nextDay);
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                string.Compare(book.Created.ToString(), nextDayText) >= 0),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                string.Compare(book.Created.ToString(), startText) >= 0),
            BookSearchOperator.LessThan => query.Where(book => string.Compare(book.Created.ToString(), startText) < 0),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                string.Compare(book.Created.ToString(), nextDayText) < 0),
            _ => query.Where(book =>
                string.Compare(book.Created.ToString(), startText) >= 0 &&
                string.Compare(book.Created.ToString(), nextDayText) < 0)
        };
    }

    private IQueryable<Book> ApplyLastModifiedDate(IQueryable<Book> query, BookSearchOperator op, DateOnly value)
    {
        (DateTimeOffset start, DateTimeOffset nextDay) = ToUtcDateBounds(value);
        if (_supportsILike)
        {
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book => book.LastModified >= nextDay),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.LastModified >= start),
                BookSearchOperator.LessThan => query.Where(book => book.LastModified < start),
                BookSearchOperator.LessThanOrEqual => query.Where(book => book.LastModified < nextDay),
                _ => query.Where(book => book.LastModified >= start && book.LastModified < nextDay)
            };
        }

        string startText = ToSqliteDateTimeOffsetString(start);
        string nextDayText = ToSqliteDateTimeOffsetString(nextDay);
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book =>
                string.Compare(book.LastModified.ToString(), nextDayText) >= 0),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book =>
                string.Compare(book.LastModified.ToString(), startText) >= 0),
            BookSearchOperator.LessThan => query.Where(book =>
                string.Compare(book.LastModified.ToString(), startText) < 0),
            BookSearchOperator.LessThanOrEqual => query.Where(book =>
                string.Compare(book.LastModified.ToString(), nextDayText) < 0),
            _ => query.Where(book =>
                string.Compare(book.LastModified.ToString(), startText) >= 0 &&
                string.Compare(book.LastModified.ToString(), nextDayText) < 0)
        };
    }

    private static (DateTimeOffset Start, DateTimeOffset NextDay) ToUtcDateBounds(DateOnly value)
    {
        var start = new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return (start, start.AddDays(1));
    }

    private static string ToSqliteDateTimeOffsetString(DateTimeOffset value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
    }
}
