namespace Infrastructure.BookCovers;

using System.Threading.Channels;

public sealed class InMemoryBookCoverQueue : IBookCoverQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask QueueAsync(Guid bookId, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(bookId, cancellationToken);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
