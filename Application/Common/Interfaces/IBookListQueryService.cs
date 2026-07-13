namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookListQueryService
{
    Task<IReadOnlyCollection<BookListItemDto>> GetBooksAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    Task<int> GetBookCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AdminBookListItemDto>> GetAdminBooksAsync(
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    Task<int> GetAdminBookCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken);

    Task<string?> GetNextCycleSortDirectionAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        string sortBy,
        string? currentSortDirection,
        CancellationToken cancellationToken);
}
