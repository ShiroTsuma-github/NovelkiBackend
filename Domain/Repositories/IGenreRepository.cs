namespace Domain.Repositories;

public interface IGenreRepository
{
    Task<Genre?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Genre?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IEnumerable<Genre>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    Task<IEnumerable<Genre>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    Task<int> GetCountAsync(CancellationToken cancellationToken);
    Task AddAsync(Genre genre, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
