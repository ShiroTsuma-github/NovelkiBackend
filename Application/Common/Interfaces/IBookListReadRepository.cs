namespace Application.Common.Interfaces;

using Application.Common;
using Application.Common.DTOs.Book;

public interface IBookListReadRepository
{
    Task<IEnumerable<BookListItemDto>> GetAllListAsync(Guid ownerId, int skip, int take, string? sortBy, string? sortDirection, CancellationToken cancellationToken);
    Task<IEnumerable<BookListItemDto>> SearchListAsync(Guid ownerId, BookSearchCriteria criteria, int skip, int take, string? sortBy, string? sortDirection, CancellationToken cancellationToken);
    Task<IEnumerable<AdminBookListItemDto>> GetAllAdminListAsync(int skip, int take, string? sortBy, string? sortDirection, CancellationToken cancellationToken);
    Task<IEnumerable<AdminBookListItemDto>> SearchAdminListAsync(BookSearchCriteria criteria, int skip, int take, string? sortBy, string? sortDirection, CancellationToken cancellationToken);
}
