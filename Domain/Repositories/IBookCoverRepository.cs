namespace Domain.Repositories;

public interface IBookCoverRepository
{
    public Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken);
    public Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken);
    public Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken);
    public Task AddAsync(BookCover cover, CancellationToken cancellationToken);
    public Task DeleteAsync(BookCover cover, CancellationToken cancellationToken);
    public Task SaveAsync(CancellationToken cancellationToken);
}
