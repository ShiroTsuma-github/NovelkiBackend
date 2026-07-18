namespace Infrastructure.Services;

public sealed class AdminLibraryService : IAdminLibraryService
{
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly ApplicationDbContext _context;
    private readonly IBookCoverStorage _storage;
    private readonly IPublicBookService? _publicBooks;

    public AdminLibraryService(
        ApplicationDbContext context,
        IBookCoverStorage storage,
        IBookListCacheInvalidator cacheInvalidator,
        IPublicBookService? publicBooks = null)
    {
        _context = context;
        _storage = storage;
        _publicBooks = publicBooks;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<AdminLibraryPurgeResult> DeleteAllBooksForOwnerAsync(Guid ownerId,
        CancellationToken cancellationToken)
    {
        if (_publicBooks is not null)
        {
            await _publicBooks.UnlistAllForOwnerAsync(ownerId, cancellationToken);
        }
        await using var
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var books = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .Select(book => new { book.Id, book.AuthorId })
            .ToListAsync(cancellationToken);
        var storagePaths = await _context.BookCovers
            .Where(cover => cover.Book.OwnerId == ownerId && cover.StoragePath != null)
            .Select(cover => cover.StoragePath)
            .ToListAsync(cancellationToken);

        if (books.Count == 0)
        {
            await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
            return new AdminLibraryPurgeResult(0, 0, 0);
        }

        var authorIds = books.Where(book => book.AuthorId.HasValue).Select(book => book.AuthorId!.Value).Distinct()
            .ToArray();
        var deletedBooks = await _context.Books
            .Where(book => book.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedTags = await _context.Tags
            .Where(tag => tag.OwnerId == ownerId && !tag.BookTags.Any())
            .ExecuteDeleteAsync(cancellationToken);

        var deletedAuthors = authorIds.Length == 0
            ? 0
            : await _context.Authors
                .Where(author => authorIds.Contains(author.Id) &&
                                 author.OwnerId == ownerId &&
                                 !author.IsPublic &&
                                 !author.Books.Any())
                .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var storagePath in storagePaths)
        {
            await _storage.DeleteIfExistsAsync(storagePath, cancellationToken);
        }

        await _cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);

        return new AdminLibraryPurgeResult(deletedBooks, deletedAuthors, deletedTags);
    }
}
