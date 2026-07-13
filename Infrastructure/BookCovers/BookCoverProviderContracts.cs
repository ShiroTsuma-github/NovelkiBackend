namespace Infrastructure.BookCovers;

public sealed record BookCoverCandidate(BookCoverSource Source, string ImageUrl);

public interface IBookCoverProvider
{
    Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken);
}
