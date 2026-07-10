namespace Application.Common.Interfaces;

public interface IBookListCacheInvalidator
{
    Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken);
}
