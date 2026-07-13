namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookExportQueryService
{
    Task<PaginatedResult<BookDto>> GetBooksForExportAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);
}
