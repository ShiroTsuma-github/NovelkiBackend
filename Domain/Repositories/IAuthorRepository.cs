namespace Domain.Repositories;

public interface IAuthorRepository
{
    public Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<Author?> GetByNameAsync(string name, CancellationToken cancellationToken);
    public Task<IEnumerable<Author>> SearchAsync(string? search, int take, CancellationToken cancellationToken);

    public Task<IEnumerable<Author>> SearchCreatedByAsync(Guid createdBy, string? search, int take,
        CancellationToken cancellationToken);

    public Task AddAsync(Author author, CancellationToken cancellationToken);
    public Task DeleteAsync(Author author, CancellationToken cancellationToken);
    public Task SaveAsync(CancellationToken cancellationToken);
}
