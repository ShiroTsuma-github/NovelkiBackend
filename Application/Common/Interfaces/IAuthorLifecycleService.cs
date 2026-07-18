namespace Application.Common.Interfaces;

public interface IAuthorLifecycleService
{
    Task<Author> SetVisibilityAsync(Guid authorId, Guid actorId, bool isAdmin, bool isPublic,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid authorId, Guid actorId, bool isAdmin, CancellationToken cancellationToken);

    Task<int> DeleteOwnedAuthorsAsync(Guid ownerId, CancellationToken cancellationToken);
}
