namespace Infrastructure.Persistence;

using Domain.Entities;

public class TypeRepository : ITypeRepository
{
    private readonly ApplicationDbContext _context;

    public TypeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Type type, CancellationToken cancellationToken)
    {
        _context.Types.Add(type);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var type = await _context.Types.FindAsync(new object[] { id }, cancellationToken);
        _context.Types.Remove(type!);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Type>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await _context.Types.Skip(Skip).Take(Take).ToListAsync(cancellationToken);
    }

    public async Task<Type?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Types.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Type?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _context.Types.FirstOrDefaultAsync(t => EF.Functions.Like(t.Name.ToLower(), name.ToLower()));
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Types.CountAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

