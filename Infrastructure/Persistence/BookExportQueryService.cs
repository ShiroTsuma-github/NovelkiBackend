namespace Infrastructure.Persistence;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;

public sealed class BookExportQueryService : IBookExportQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly BookSearchCriteriaApplier _criteriaApplier;
    private readonly BookSortBuilder _sortBuilder;

    public BookExportQueryService(
        ApplicationDbContext context,
        BookSearchCriteriaApplier criteriaApplier,
        BookSortBuilder sortBuilder)
    {
        _context = context;
        _criteriaApplier = criteriaApplier;
        _sortBuilder = sortBuilder;
    }

    public async Task<PaginatedResult<BookDto>> GetBooksForExportAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(BookQueryInclude.IncludeDetails(_context.Books).Where(book => book.OwnerId == ownerId), criteria);
        var books = await _sortBuilder.ToSortedPageAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var total = await ApplyCriteria(_context.Books.Where(book => book.OwnerId == ownerId), criteria).CountAsync(cancellationToken);

        return new PaginatedResult<BookDto>
        {
            Skip = skip,
            Take = take,
            Total = total,
            Data = books.Select(book => book.ToDto()).ToList()
        };
    }

    private IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        return criteria.HasFilters ? _criteriaApplier.Apply(query, criteria) : query;
    }
}
