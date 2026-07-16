namespace Infrastructure.Persistence;

using Application.Common;

public class GenreRepository : IGenreRepository
{
    private readonly ApplicationDbContext _context;

    public GenreRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Genre?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Genres.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Genre?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        string normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.Genres.FirstOrDefaultAsync(g => g.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task<IEnumerable<Genre>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await _context.Genres.Skip(Skip).Take(Take).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Genre>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        return await _context.Genres.Where(g => idList.Contains(g.Id)).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Genre genre, CancellationToken cancellationToken)
    {
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Genre? genre = await _context.Genres.FindAsync(new object[] { id }, cancellationToken);
        if (genre != null)
        {
            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Genres.CountAsync(cancellationToken);
    }
}
