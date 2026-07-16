namespace Infrastructure.BookCovers;

public sealed record BookCoverCandidate(BookCoverSource Source, string ImageUrl);

public interface IBookCoverProvider
{
    public Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken);
}
