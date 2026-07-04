namespace Domain.Repositories;

public interface IStatusRepository
{
    Task<Status?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Status?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IEnumerable<Status>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken);
    Task<int> GetCountAsync(CancellationToken cancellationToken);
    Task AddAsync(Status status, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
