namespace Domain.Repositories;

public interface IAuthorRepository
{
    Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Author?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IEnumerable<Author>> SearchAsync(string? search, int take, CancellationToken cancellationToken);
    Task AddAsync(Author author, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
