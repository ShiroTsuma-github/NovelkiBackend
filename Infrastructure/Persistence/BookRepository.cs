namespace Infrastructure.Persistence;

public class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _context;

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetByNameAsync(string name, Guid ownerId, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(name);
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(
                b => b.OwnerId == ownerId &&
                     (b.NormalizedPrimaryTitle == normalizedName ||
                      b.Titles.Any(t => t.NormalizedTitle == normalizedName)),
                cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .Where(b => b.OwnerId == ownerId)
            .OrderBy(b => b.PrimaryTitle)
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
        if (book != null)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(b => b.OwnerId == ownerId, cancellationToken);
    }

    private static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
    {
        return query
            .Include(b => b.Author).ThenInclude(a => a!.Names)
            .Include(b => b.ContentType)
            .Include(b => b.Status)
            .Include(b => b.Titles)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .Include(b => b.Links)
            .Include(b => b.ProgressHistory);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
