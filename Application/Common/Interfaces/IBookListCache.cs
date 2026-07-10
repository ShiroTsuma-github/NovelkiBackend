namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookListCache
{
    Task<PaginatedResult<BookDto>?> GetBooksAsync(
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
        PaginatedResult<BookDto> value,
        CancellationToken cancellationToken);
}
