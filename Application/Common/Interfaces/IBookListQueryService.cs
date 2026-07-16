namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookListQueryService
{
    public Task<IReadOnlyCollection<BookListItemDto>> GetBooksAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    public Task<int> GetBookCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<AdminBookListItemDto>> GetAdminBooksAsync(
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    public Task<int> GetAdminBookCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken);

    public Task<string?> GetNextCycleSortDirectionAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        string sortBy,
        string? currentSortDirection,
        CancellationToken cancellationToken);
}
