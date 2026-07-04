namespace Domain.Repositories;

public interface ITagRepository
{
    Task<Tag?> GetByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken);
    Task<IEnumerable<Tag>> GetByNamesAsync(Guid ownerId, IEnumerable<string> names, CancellationToken cancellationToken);
    Task<IEnumerable<Tag>> SearchAsync(Guid ownerId, string? search, int take, CancellationToken cancellationToken);
    Task AddAsync(Tag tag, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
