namespace Application.Common.Interfaces;

public interface IGlobalTagService
{
    Task<IReadOnlyCollection<Tag>> SearchAsync(string? search, int take, CancellationToken cancellationToken);
    Task<Tag> CreateAsync(string name, string? description, CancellationToken cancellationToken);
    Task<Tag> UpdateAsync(Guid id, string name, string? description, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
