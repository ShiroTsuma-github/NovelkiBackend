namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookListCache
{
    Task<PaginatedResult<BookListItemDto>?> GetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    Task SetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        PaginatedResult<BookListItemDto> value,
        CancellationToken cancellationToken);
}
