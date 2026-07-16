namespace Infrastructure.Persistence;

using Domain.Entities;
using System.Linq.Expressions;

public sealed class BookSearchCriteriaApplier
{
    private readonly DateSearchExpressionFactory _dateSearch;
    private readonly NumberSearchExpressionFactory _numberSearch;
    private readonly TextSearchExpressionFactory _textSearch;

    public BookSearchCriteriaApplier(ApplicationDbContext context)
    {
        var isPostgres = context.Database.IsNpgsql();
        _dateSearch = new DateSearchExpressionFactory(isPostgres);
        _numberSearch = new NumberSearchExpressionFactory(isPostgres);
        _textSearch = new TextSearchExpressionFactory(isPostgres);
    }

    public IQueryable<Book> Apply(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        var predicate = PredicateExpression.AndAll(BuildPredicates(criteria));
        return predicate == null ? query : query.Where(predicate);
    }

    private IEnumerable<Expression<Func<Book, bool>>> BuildPredicates(BookSearchCriteria criteria)
    {
        return criteria.Terms
            .Select(BuildGeneralTextPredicate)
            .Concat(criteria.Fields
                .Select(BuildFieldPredicate)
                .OfType<Expression<Func<Book, bool>>>())
            .Concat(criteria.Numbers
                .Select(BuildNumberPredicate)
                .OfType<Expression<Func<Book, bool>>>())
            .Concat(criteria.Dates
                .Select(BuildDatePredicate)
                .OfType<Expression<Func<Book, bool>>>())
            .Concat(criteria.Missing
                .Select(BuildMissingPredicate)
                .OfType<Expression<Func<Book, bool>>>());
    }

    private Expression<Func<Book, bool>> BuildGeneralTextPredicate(string search)
    {
        return PredicateExpression.Or(
            BuildTitlePredicate(search),
            BuildAuthorPredicate(search));
    }

    private Expression<Func<Book, bool>>? BuildFieldPredicate(BookSearchFieldFilter filter)
    {
        return filter.Field switch
        {
            BookSearchField.Title => MatchAny(filter.Values, BuildTitlePredicate),
            BookSearchField.Author => MatchAny(filter.Values, BuildAuthorPredicate),
            BookSearchField.Tag => MatchAny(filter.Values, BuildTagPredicate),
            BookSearchField.Genre => MatchAny(filter.Values, BuildGenrePredicate),
            BookSearchField.Status => MatchAny(filter.Values, BuildStatusPredicate),
            BookSearchField.Type => MatchAny(filter.Values, BuildTypePredicate),
            _ => null
        };
    }

    private Expression<Func<Book, bool>>? BuildNumberPredicate(BookSearchNumberFilter filter)
    {
        return filter.Field switch
        {
            BookSearchNumberField.Rating =>
                _numberSearch.MatchInteger<Book>(book => book.Rating, filter.Operator, filter.Value),
            BookSearchNumberField.Priority =>
                _numberSearch.MatchInteger<Book>(book => book.Priority, filter.Operator, filter.Value),
            BookSearchNumberField.CurrentChapter =>
                _numberSearch.MatchDecimal<Book>(book => book.CurrentChapterNumber, filter.Operator, filter.Value),
            BookSearchNumberField.TotalChapters =>
                _numberSearch.MatchDecimal<Book>(book => book.TotalChapters, filter.Operator, filter.Value),
            _ => null
        };
    }

    private Expression<Func<Book, bool>>? BuildDatePredicate(BookSearchDateFilter filter)
    {
        return filter.Field switch
        {
            BookSearchDateField.Created =>
                _dateSearch.Match<Book>(book => book.Created, filter.Operator, filter.Value),
            BookSearchDateField.LastModified =>
                _dateSearch.Match<Book>(book => book.LastModified, filter.Operator, filter.Value),
            _ => null
        };
    }

    private static Expression<Func<Book, bool>>? BuildMissingPredicate(BookSearchMissingFilter filter)
    {
        return filter.Field switch
        {
            BookSearchMissingField.Rating => book => book.Rating == null,
            BookSearchMissingField.Priority => book => book.Priority == null,
            BookSearchMissingField.Author => book => book.Author == null,
            BookSearchMissingField.Genre => book => !book.BookGenres.Any(),
            BookSearchMissingField.Tag => book => !book.BookTags.Any(),
            BookSearchMissingField.CurrentChapter => book => book.CurrentChapterNumber == null,
            BookSearchMissingField.TotalChapters => book => book.TotalChapters == null,
            BookSearchMissingField.Cover => book => book.Cover == null,
            BookSearchMissingField.Link => book => !book.Links.Any(),
            _ => null
        };
    }

    private Expression<Func<Book, bool>> BuildTitlePredicate(string search)
    {
        return PredicateExpression.Or(
            _textSearch.Match<Book>(
                book => book.PrimaryTitle,
                book => book.NormalizedPrimaryTitle,
                search),
            _textSearch.AnyMatch<Book, BookTitle>(
                book => book.Titles,
                title => title.Title,
                title => title.NormalizedTitle,
                search));
    }

    private Expression<Func<Book, bool>> BuildAuthorPredicate(string search)
    {
        return PredicateExpression.And<Book>(
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
                    search)));
    }

    private Expression<Func<Book, bool>> BuildTagPredicate(string search)
    {
        return _textSearch.AnyMatch<Book, BookTag>(
            book => book.BookTags,
            bookTag => bookTag.Tag.Name,
            bookTag => bookTag.Tag.NormalizedName,
            search);
    }

    private Expression<Func<Book, bool>> BuildGenrePredicate(string search)
    {
        return _textSearch.AnyMatch<Book, BookGenre>(
            book => book.BookGenres,
            bookGenre => bookGenre.Genre.Name,
            bookGenre => bookGenre.Genre.NormalizedName,
            search);
    }

    private Expression<Func<Book, bool>> BuildStatusPredicate(string search)
    {
        return _textSearch.Match<Book>(
            book => book.Status.Name,
            book => book.Status.Name.ToUpper(),
            search);
    }

    private Expression<Func<Book, bool>> BuildTypePredicate(string search)
    {
        return _textSearch.Match<Book>(
            book => book.ContentType.Name,
            book => book.ContentType.Name.ToUpper(),
            search);
    }

    private static Expression<Func<Book, bool>>? MatchAny(
        IEnumerable<string> searches,
        Func<string, Expression<Func<Book, bool>>> predicateFactory)
    {
        return PredicateExpression.OrAll(searches.Select(predicateFactory));
    }
}
