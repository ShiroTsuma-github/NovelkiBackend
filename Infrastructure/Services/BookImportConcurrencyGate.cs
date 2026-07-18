namespace Infrastructure.Services;

using Microsoft.Extensions.Options;

public sealed class BookImportConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore;

    public BookImportConcurrencyGate(IOptions<BookImportSecurityOptions> options)
    {
        var limit = Math.Max(1, options.Value.MaxConcurrentFullImportOperations);
        _semaphore = new SemaphoreSlim(limit, limit);
    }

    public Lease TryAcquire()
    {
        if (!_semaphore.Wait(0))
        {
            throw new FullImportCapacityExceededException(
                "The server is already processing the maximum number of full imports. Try again later.");
        }

        return new Lease(_semaphore);
    }

    public sealed class Lease : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        public Lease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}
