namespace Infrastructure.Services;

using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public sealed class AdminLibraryService : IAdminLibraryService
{
    private readonly ApplicationDbContext _context;
    private readonly IBookCoverStorage _storage;
    private readonly IBookListCacheInvalidator _cacheInvalidator;

    public AdminLibraryService(
        ApplicationDbContext context,
        IBookCoverStorage storage,
        IBookListCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _storage = storage;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<AdminLibraryPurgeResult> DeleteAllBooksForOwnerAsync(Guid ownerId,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var books = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .Select(book => new { book.Id, book.AuthorId })
            .ToListAsync(cancellationToken);
        List<string?> storagePaths = await _context.BookCovers
            .Where(cover => cover.Book.OwnerId == ownerId && cover.StoragePath != null)
            .Select(cover => cover.StoragePath)
            .ToListAsync(cancellationToken);

        if (books.Count == 0)
        {
            await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
            return new AdminLibraryPurgeResult(0, 0, 0);
        }

        Guid[] authorIds = books.Where(book => book.AuthorId.HasValue).Select(book => book.AuthorId!.Value).Distinct()
            .ToArray();
        int deletedBooks = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

        int deletedTags = await _context.Tags
            .Where(tag => tag.OwnerId == ownerId && !tag.BookTags.Any())
            .ExecuteDeleteAsync(cancellationToken);

        int deletedAuthors = authorIds.Length == 0
            ? 0
            : await _context.Authors
                .Where(author => authorIds.Contains(author.Id) && !author.Books.Any())
                .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (string? storagePath in storagePaths)
        {
            await _storage.DeleteIfExistsAsync(storagePath, cancellationToken);
        }

        await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);

        return new AdminLibraryPurgeResult(deletedBooks, deletedAuthors, deletedTags);
    }
}
