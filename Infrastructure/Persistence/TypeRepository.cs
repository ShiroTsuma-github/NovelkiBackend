namespace Infrastructure.Persistence;

using Application.Common;

public class TypeRepository : ITypeRepository
{
    private readonly ApplicationDbContext _context;

    public TypeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ContentType type, CancellationToken cancellationToken)
    {
        _context.ContentTypes.Add(type);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var type = await _context.ContentTypes.FindAsync(new object[] { id }, cancellationToken);
        if (type != null)
        {
            _context.ContentTypes.Remove(type);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<ContentType>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await _context.ContentTypes.OrderBy(t => t.Name).Skip(Skip).Take(Take).ToListAsync(cancellationToken);
    }

    public async Task<ContentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ContentTypes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<ContentType?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.ContentTypes.FirstOrDefaultAsync(
            t => t.Name.ToUpper() == normalizedName || t.Slug.ToUpper() == normalizedName,
            cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.ContentTypes.CountAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
