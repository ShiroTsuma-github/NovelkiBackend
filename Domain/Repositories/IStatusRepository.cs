namespace Domain.Repositories;

public interface IStatusRepository
{
    public Task<Status?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<Status?> GetByNameAsync(string name, CancellationToken cancellationToken);
    public Task<IEnumerable<Status>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    public Task<int> GetCountAsync(CancellationToken cancellationToken);
    public Task AddAsync(Status status, CancellationToken cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    public Task SaveAsync(CancellationToken cancellationToken);
}
