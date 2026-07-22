namespace Application.Common.Interfaces;

public interface IBookCoverOperationGate
{
    public Task<IAsyncDisposable> EnterAsync(Guid bookId, CancellationToken cancellationToken);
}

public sealed class NoopBookCoverOperationGate : IBookCoverOperationGate
{
    public static readonly NoopBookCoverOperationGate Instance = new();

    public Task<IAsyncDisposable> EnterAsync(Guid bookId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IAsyncDisposable>(NoopLease.Instance);
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public static readonly NoopLease Instance = new();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
