namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Entities;
using System.Globalization;
using System.Linq.Expressions;

public sealed class BookSearchCriteriaApplier
{
    private readonly bool _supportsILike;

    public BookSearchCriteriaApplier(ApplicationDbContext context)
    {
        _supportsILike = context.Database.IsNpgsql();
    }

    public IQueryable<Book> Apply(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        foreach (var term in criteria.Terms)
        {
            query = ApplyGeneralTextSearch(query, term);
        }

        foreach (var filter in criteria.Fields)
        {
            query = filter.Field switch
            {
                BookSearchField.Title => ApplyTitleSearch(query, filter.Values),
                BookSearchField.Author => ApplyAuthorSearch(query, filter.Values),
                BookSearchField.Tag => ApplyTagSearch(query, filter.Values),
                BookSearchField.Genre => ApplyGenreSearch(query, filter.Values),
                BookSearchField.Status => ApplyStatusSearch(query, filter.Values),
                BookSearchField.Type => ApplyTypeSearch(query, filter.Values),
                _ => query
            };
        }

        foreach (var filter in criteria.Numbers)
        {
            query = filter.Field switch
            {
                BookSearchNumberField.Rating => ApplyRating(query, filter.Operator, filter.Value),
                BookSearchNumberField.Priority => ApplyPriority(query, filter.Operator, filter.Value),
                BookSearchNumberField.CurrentChapter => ApplyCurrentChapter(query, filter.Operator, filter.Value),
                BookSearchNumberField.TotalChapters => ApplyTotalChapters(query, filter.Operator, filter.Value),
                _ => query
            };
        }

        foreach (var filter in criteria.Dates)
        {
            query = filter.Field switch
            {
                BookSearchDateField.Created => ApplyCreatedDate(query, filter.Operator, filter.Value),
                BookSearchDateField.LastModified => ApplyLastModifiedDate(query, filter.Operator, filter.Value),
                _ => query
            };
        }

        foreach (var filter in criteria.Missing)
        {
            query = filter.Field switch
            {
                BookSearchMissingField.Rating => query.Where(book => book.Rating == null),
                BookSearchMissingField.Priority => query.Where(book => book.Priority == null),
                BookSearchMissingField.Author => query.Where(book => book.Author == null),
                BookSearchMissingField.Genre => query.Where(book => !book.BookGenres.Any()),
                BookSearchMissingField.Tag => query.Where(book => !book.BookTags.Any()),
                BookSearchMissingField.CurrentChapter => query.Where(book => book.CurrentChapterNumber == null),
                BookSearchMissingField.TotalChapters => query.Where(book => book.TotalChapters == null),
                BookSearchMissingField.Cover => query.Where(book => book.Cover == null),
                BookSearchMissingField.Link => query.Where(book => !book.Links.Any()),
                _ => query
            };
        }

        return query;
    }

    private IQueryable<Book> ApplyGeneralTextSearch(IQueryable<Book> query, string term)
    {
        var pattern = ToLikePattern(term);
        if (_supportsILike)
        {
            return query.Where(book =>
                EF.Functions.ILike(book.PrimaryTitle, pattern, @"\") ||
                book.Titles.Any(title => EF.Functions.ILike(title.Title, pattern, @"\")) ||
                (book.Author != null && (
                    EF.Functions.ILike(book.Author.PrimaryName, pattern, @"\") ||
                    book.Author.Names.Any(name => EF.Functions.ILike(name.Name, pattern, @"\")))));
        }

        var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(term));
        return query.Where(book =>
            EF.Functions.Like(book.NormalizedPrimaryTitle, normalizedPattern, @"\") ||
            book.Titles.Any(title => EF.Functions.Like(title.NormalizedTitle, normalizedPattern, @"\")) ||
            (book.Author != null && (
                EF.Functions.Like(book.Author.NormalizedPrimaryName, normalizedPattern, @"\") ||
                book.Author.Names.Any(name => EF.Functions.Like(name.NormalizedName, normalizedPattern, @"\")))));
    }

    private IQueryable<Book> ApplyTitleSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book =>
                    EF.Functions.ILike(book.PrimaryTitle, pattern, @"\") ||
                    book.Titles.Any(title => EF.Functions.ILike(title.Title, pattern, @"\"));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book =>
                    EF.Functions.Like(book.NormalizedPrimaryTitle, normalizedPattern, @"\") ||
                    book.Titles.Any(title => EF.Functions.Like(title.NormalizedTitle, normalizedPattern, @"\"));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyAuthorSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.Author != null && (
                    EF.Functions.ILike(book.Author.PrimaryName, pattern, @"\") ||
                    book.Author.Names.Any(name => EF.Functions.ILike(name.Name, pattern, @"\")));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.Author != null && (
                    EF.Functions.Like(book.Author.NormalizedPrimaryName, normalizedPattern, @"\") ||
                    book.Author.Names.Any(name => EF.Functions.Like(name.NormalizedName, normalizedPattern, @"\")));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTagSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.BookTags.Any(bookTag => EF.Functions.ILike(bookTag.Tag.Name, pattern, @"\"));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.BookTags.Any(bookTag => EF.Functions.Like(bookTag.Tag.NormalizedName, normalizedPattern, @"\"));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyGenreSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => book.BookGenres.Any(bookGenre => EF.Functions.ILike(bookGenre.Genre.Name, pattern, @"\"));
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => book.BookGenres.Any(bookGenre => EF.Functions.Like(bookGenre.Genre.NormalizedName, normalizedPattern, @"\"));
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyStatusSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => EF.Functions.ILike(book.Status.Name, pattern, @"\");
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => EF.Functions.Like(book.Status.Name.ToUpper(), normalizedPattern, @"\");
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private IQueryable<Book> ApplyTypeSearch(IQueryable<Book> query, IReadOnlyCollection<string> searches)
    {
        var predicates = _supportsILike
            ? searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var pattern = ToLikePattern(search);
                return book => EF.Functions.ILike(book.ContentType.Name, pattern, @"\");
            })
            : searches.Select<string, Expression<Func<Book, bool>>>(search =>
            {
                var normalizedPattern = ToLikePattern(MappingExtensions.NormalizeName(search));
                return book => EF.Functions.Like(book.ContentType.Name.ToUpper(), normalizedPattern, @"\");
            });

        return ApplyAnyFieldMatch(query, predicates);
    }

    private static IQueryable<Book> ApplyAnyFieldMatch(
        IQueryable<Book> query,
        IEnumerable<Expression<Func<Book, bool>>> predicates)
    {
        Expression<Func<Book, bool>>? combined = null;

        foreach (var predicate in predicates)
        {
            combined = combined == null ? predicate : OrElse(combined, predicate);
        }

        return combined == null ? query : query.Where(combined);
    }

    private static Expression<Func<Book, bool>> OrElse(
        Expression<Func<Book, bool>> left,
        Expression<Func<Book, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Book), "book");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<Book, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }

    private static string ToLikePattern(string value)
    {
        var trimmed = value.Trim();
        var pattern = EscapeLike(trimmed).Replace("*", "%", StringComparison.Ordinal);
        return trimmed.Contains('*') ? pattern : $"%{pattern}%";
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private IQueryable<Book> ApplyRating(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        if (!_supportsILike)
        {
            var sqliteValue = (double)value;
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book => book.Rating != null && book.Rating.Value > sqliteValue),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Rating != null && book.Rating.Value >= sqliteValue),
                BookSearchOperator.LessThan => query.Where(book => book.Rating != null && book.Rating.Value < sqliteValue),
                BookSearchOperator.LessThanOrEqual => query.Where(book => book.Rating != null && book.Rating.Value <= sqliteValue),
                _ => query.Where(book => book.Rating != null && book.Rating.Value == sqliteValue)
            };
        }

        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.Rating != null && (decimal)book.Rating.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Rating != null && (decimal)book.Rating.Value >= value),
            BookSearchOperator.LessThan => query.Where(book => book.Rating != null && (decimal)book.Rating.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.Rating != null && (decimal)book.Rating.Value <= value),
            _ => query.Where(book => book.Rating != null && (decimal)book.Rating.Value == value)
        };
    }

    private IQueryable<Book> ApplyPriority(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        if (!_supportsILike)
        {
            var sqliteValue = (double)value;
            return op switch
            {
                BookSearchOperator.GreaterThan => query.Where(book => book.Priority != null && book.Priority.Value > sqliteValue),
                BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Priority != null && book.Priority.Value >= sqliteValue),
                BookSearchOperator.LessThan => query.Where(book => book.Priority != null && book.Priority.Value < sqliteValue),
                BookSearchOperator.LessThanOrEqual => query.Where(book => book.Priority != null && book.Priority.Value <= sqliteValue),
                _ => query.Where(book => book.Priority != null && book.Priority.Value == sqliteValue)
            };
        }

        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.Priority != null && (decimal)book.Priority.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.Priority != null && (decimal)book.Priority.Value >= value),
            BookSearchOperator.LessThan => query.Where(book => book.Priority != null && (decimal)book.Priority.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.Priority != null && (decimal)book.Priority.Value <= value),
            _ => query.Where(book => book.Priority != null && (decimal)book.Priority.Value == value)
        };
    }

    private static IQueryable<Book> ApplyCurrentChapter(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber >= value),
            BookSearchOperator.LessThan => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber <= value),
            _ => query.Where(book => book.CurrentChapterNumber != null && book.CurrentChapterNumber == value)
        };
    }

    private static IQueryable<Book> ApplyTotalChapters(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => book.TotalChapters != null && book.TotalChapters > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => book.TotalChapters != null && book.TotalChapters >= value),
            BookSearchOperator.LessThan => query.Where(book => book.TotalChapters != null && book.TotalChapters < value),
            BookSearchOperator.LessThanOrEqual => query.Where(book => book.TotalChapters != null && book.TotalChapters <= value),
            _ => query.Where(book => book.TotalChapters != null && book.TotalChapters == value)
        };
    }

    private IQueryable<Book> ApplyCreatedDate(IQueryable<Book> query, BookSearchOperator op, DateOnly value)
    {
        var (start, nextDay) = ToUtcDateBounds(value);
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

        var startText = ToSqliteDateTimeOffsetString(start);
        var nextDayText = ToSqliteDateTimeOffsetString(nextDay);
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => string.Compare(book.Created.ToString(), nextDayText) >= 0),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => string.Compare(book.Created.ToString(), startText) >= 0),
            BookSearchOperator.LessThan => query.Where(book => string.Compare(book.Created.ToString(), startText) < 0),
            BookSearchOperator.LessThanOrEqual => query.Where(book => string.Compare(book.Created.ToString(), nextDayText) < 0),
            _ => query.Where(book => string.Compare(book.Created.ToString(), startText) >= 0 && string.Compare(book.Created.ToString(), nextDayText) < 0)
        };
    }

    private IQueryable<Book> ApplyLastModifiedDate(IQueryable<Book> query, BookSearchOperator op, DateOnly value)
    {
        var (start, nextDay) = ToUtcDateBounds(value);
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

        var startText = ToSqliteDateTimeOffsetString(start);
        var nextDayText = ToSqliteDateTimeOffsetString(nextDay);
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(book => string.Compare(book.LastModified.ToString(), nextDayText) >= 0),
            BookSearchOperator.GreaterThanOrEqual => query.Where(book => string.Compare(book.LastModified.ToString(), startText) >= 0),
            BookSearchOperator.LessThan => query.Where(book => string.Compare(book.LastModified.ToString(), startText) < 0),
            BookSearchOperator.LessThanOrEqual => query.Where(book => string.Compare(book.LastModified.ToString(), nextDayText) < 0),
            _ => query.Where(book => string.Compare(book.LastModified.ToString(), startText) >= 0 && string.Compare(book.LastModified.ToString(), nextDayText) < 0)
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
