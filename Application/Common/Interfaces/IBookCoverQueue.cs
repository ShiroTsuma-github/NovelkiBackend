namespace Application.Common.Interfaces;

public interface IBookCoverQueue
{
    ValueTask QueueAsync(Guid bookId, CancellationToken cancellationToken);
}
