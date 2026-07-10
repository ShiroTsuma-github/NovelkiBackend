namespace Infrastructure.Services;

using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

public sealed class AdminLibraryService : IAdminLibraryService
{
    private readonly ApplicationDbContext _context;
    private readonly IBookListCacheInvalidator _cacheInvalidator;

    public AdminLibraryService(ApplicationDbContext context, IBookListCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<AdminLibraryPurgeResult> DeleteAllBooksForOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var books = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .Select(book => new { book.Id, book.AuthorId })
            .ToListAsync(cancellationToken);

        if (books.Count == 0)
        {
            await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
            return new AdminLibraryPurgeResult(0, 0, 0);
        }

        var authorIds = books.Where(book => book.AuthorId.HasValue).Select(book => book.AuthorId!.Value).Distinct().ToArray();
        var deletedBooks = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedTags = await _context.Tags
            .Where(tag => tag.OwnerId == ownerId && !tag.BookTags.Any())
            .ExecuteDeleteAsync(cancellationToken);

        var deletedAuthors = authorIds.Length == 0
            ? 0
            : await _context.Authors
                .Where(author => authorIds.Contains(author.Id) && !author.Books.Any())
                .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);

        return new AdminLibraryPurgeResult(deletedBooks, deletedAuthors, deletedTags);
    }
}
