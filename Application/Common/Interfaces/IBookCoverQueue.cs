namespace Application.Common.Interfaces;

public interface IBookCoverQueue
{
    public ValueTask QueueAsync(Guid bookId, CancellationToken cancellationToken);
}
