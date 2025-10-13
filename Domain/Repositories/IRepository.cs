namespace Domain.Repositories;

public interface IRepository<TEntity, TId> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken);
    Task<TEntity?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IEnumerable<TEntity>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);
    Task DeleteAsync(TId id, CancellationToken cancellationToken);
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}
