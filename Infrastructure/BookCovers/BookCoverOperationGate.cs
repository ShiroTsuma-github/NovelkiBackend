namespace Infrastructure.BookCovers;

public sealed class BookCoverOperationGate : IBookCoverOperationGate
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public async Task<IAsyncDisposable> EnterAsync(Guid bookId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(bookId, static _ => new Entry());
            lock (entry)
            {
                if (!_entries.TryGetValue(bookId, out var current) || !ReferenceEquals(entry, current))
                {
                    continue;
                }

                entry.References++;
            }

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
                return new Lease(this, bookId, entry);
            }
            catch
            {
                ReleaseReference(bookId, entry, false);
                throw;
            }
        }
    }

    private void ReleaseReference(Guid bookId, Entry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (entry)
        {
            entry.References--;
            if (entry.References == 0)
            {
                _entries.TryRemove(new KeyValuePair<Guid, Entry>(bookId, entry));
            }
        }
    }

    private sealed class Entry
    {
        public int References { get; set; }
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly Guid _bookId;
        private readonly Entry _entry;
        private BookCoverOperationGate? _owner;

        public Lease(BookCoverOperationGate owner, Guid bookId, Entry entry)
        {
            _owner = owner;
            _bookId = bookId;
            _entry = entry;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _owner, null)?.ReleaseReference(_bookId, _entry, true);
            return ValueTask.CompletedTask;
        }
    }
}
