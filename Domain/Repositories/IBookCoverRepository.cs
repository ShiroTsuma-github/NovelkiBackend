namespace Domain.Repositories;

public interface IBookCoverRepository
{
    Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken);
    Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken);
    Task AddAsync(BookCover cover, CancellationToken cancellationToken);
    Task DeleteAsync(BookCover cover, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
