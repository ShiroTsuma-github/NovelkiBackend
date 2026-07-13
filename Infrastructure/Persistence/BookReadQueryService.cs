namespace Infrastructure.Persistence;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Domain.Entities;

public sealed class BookReadQueryService : IBookListQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly BookSearchCriteriaApplier _criteriaApplier;
    private readonly BookSortBuilder _sortBuilder;
    private readonly BookListProjectionQuery _projectionQuery;

    public BookReadQueryService(
        ApplicationDbContext context,
        BookSearchCriteriaApplier criteriaApplier,
        BookSortBuilder sortBuilder,
        BookListProjectionQuery projectionQuery)
    {
        _context = context;
        _criteriaApplier = criteriaApplier;
        _sortBuilder = sortBuilder;
        _projectionQuery = projectionQuery;
    }

    public async Task<IReadOnlyCollection<BookListItemDto>> GetBooksAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(CreateOwnerQuery(ownerId), criteria);
        return await _projectionQuery.GetBooksAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
    }

    public Task<int> GetBookCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(_context.Books.Where(book => book.OwnerId == ownerId), criteria);
        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminBookListItemDto>> GetAdminBooksAsync(
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(_context.Books.AsNoTracking(), criteria);
        return await _projectionQuery.GetAdminBooksAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
    }

    public Task<int> GetAdminBookCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(_context.Books, criteria);
        return query.CountAsync(cancellationToken);
    }

    public async Task<string?> GetNextCycleSortDirectionAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        string sortBy,
        string? currentSortDirection,
        CancellationToken cancellationToken)
    {
        var query = ApplyCriteria(BookQueryInclude.IncludeDetails(_context.Books).Where(book => book.OwnerId == ownerId), criteria);
        return await _sortBuilder.GetNextCycleSortDirectionAsync(query, sortBy, currentSortDirection, cancellationToken);
    }

    private IQueryable<Book> CreateOwnerQuery(Guid ownerId)
    {
        return _context.Books.AsNoTracking().Where(book => book.OwnerId == ownerId);
    }

    private IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        return criteria.HasFilters ? _criteriaApplier.Apply(query, criteria) : query;
    }
}
