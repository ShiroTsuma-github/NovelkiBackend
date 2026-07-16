namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookListCache
{
    public Task<PaginatedResult<BookListItemDto>?> GetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    public Task SetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        PaginatedResult<BookListItemDto> value,
        CancellationToken cancellationToken);
}
