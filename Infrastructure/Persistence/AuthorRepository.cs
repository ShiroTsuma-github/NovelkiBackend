namespace Infrastructure.Persistence;

public class AuthorRepository : IAuthorRepository
{
    private readonly ApplicationDbContext _context;

    public AuthorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Authors
            .Include(a => a.Names)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Author?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(name);
        return await _context.Authors
            .Include(a => a.Names)
            .FirstOrDefaultAsync(
                a => a.NormalizedPrimaryName == normalizedName ||
                     a.Names.Any(n => n.NormalizedName == normalizedName),
                cancellationToken);
    }

    public async Task<IEnumerable<Author>> SearchAsync(string? search, int take, CancellationToken cancellationToken)
    {
        var query = _context.Authors.Include(a => a.Names).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = Normalize(search);
            query = query.Where(a =>
                a.NormalizedPrimaryName.Contains(normalizedSearch) ||
                a.Names.Any(n => n.NormalizedName.Contains(normalizedSearch)));
        }

        return await query.OrderBy(a => a.PrimaryName).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Author author, CancellationToken cancellationToken)
    {
        _context.Authors.Add(author);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
