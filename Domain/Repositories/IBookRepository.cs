namespace Domain.Repositories;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);
    Task<Book?> GetByNameAsync(string name, Guid ownerId, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken);
    Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken);
    Task AddAsync(Book book, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
