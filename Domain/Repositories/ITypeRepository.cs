namespace Domain.Repositories;

public interface ITypeRepository
{
    public Task<ContentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<ContentType?> GetByNameAsync(string name, CancellationToken cancellationToken);
    public Task<IEnumerable<ContentType>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    public Task<int> GetCountAsync(CancellationToken cancellationToken);
    public Task AddAsync(ContentType type, CancellationToken cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    public Task SaveAsync(CancellationToken cancellationToken);
}
