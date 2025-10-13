namespace Infrastructure.Persistence;

public class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _context;

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Include(b => b.Author)
            .Include(b => b.Type)
            .Include(b => b.Status)
            .Include(b => b.Genres)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }
    public async Task<Book?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _context.Books.FirstOrDefaultAsync(b => b.Title == name, cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Skip(Skip)
            .Take(Take)
            .Include(b => b.Author)
            .Include(b => b.Type)
            .Include(b => b.Status)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync(new object[] { id }, cancellationToken);
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

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(cancellationToken);
    }
}
