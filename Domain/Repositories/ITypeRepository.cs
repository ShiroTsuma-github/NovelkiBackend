namespace Domain.Repositories;

public interface ITypeRepository
{
    Task<ContentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ContentType?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IEnumerable<ContentType>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    Task<int> GetCountAsync(CancellationToken cancellationToken);
    Task AddAsync(ContentType type, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
