namespace Domain.Repositories;

public interface IGenreRepository
{
    public Task<Genre?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<Genre?> GetByNameAsync(string name, CancellationToken cancellationToken);
    public Task<IEnumerable<Genre>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    public Task<IEnumerable<Genre>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    public Task<int> GetCountAsync(CancellationToken cancellationToken);
    public Task AddAsync(Genre genre, CancellationToken cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    public Task SaveAsync(CancellationToken cancellationToken);
}
