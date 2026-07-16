namespace Application.Common.Interfaces;

public interface IBookListCacheInvalidator
{
    public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken);
}
