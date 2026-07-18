namespace Application.Common.Interfaces;

public interface IStorageCleanupQueue
{
    Task EnqueueAsync(IEnumerable<string?> storagePaths, CancellationToken cancellationToken);
}
