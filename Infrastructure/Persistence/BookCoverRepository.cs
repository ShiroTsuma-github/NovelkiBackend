namespace Infrastructure.Persistence;

public class BookCoverRepository : IBookCoverRepository
{
    private readonly ApplicationDbContext _context;

    public BookCoverRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken)
    {
        return _context.BookCovers
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.BookId == bookId && c.Book.OwnerId == ownerId, cancellationToken);
    }

    public Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken)
    {
        return _context.BookCovers
            .Include(c => c.Book)
            .ThenInclude(b => b.Titles)
            .Include(c => c.Book)
            .ThenInclude(b => b.Links)
            .FirstOrDefaultAsync(c => c.BookId == bookId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken)
    {
        DateTimeOffset retryBefore = DateTimeOffset.UtcNow.AddMinutes(-15);
        return await _context.BookCovers
            .Include(c => c.Book)
            .ThenInclude(b => b.Titles)
            .Include(c => c.Book)
            .ThenInclude(b => b.Links)
            .Where(c => c.Status == BookCoverStatus.Pending &&
                        (c.LastAttemptAt == null || c.LastAttemptAt < retryBefore))
            .OrderBy(c => c.Created)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BookCover cover, CancellationToken cancellationToken)
    {
        _context.BookCovers.Add(cover);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BookCover cover, CancellationToken cancellationToken)
    {
        _context.BookCovers.Remove(cover);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
