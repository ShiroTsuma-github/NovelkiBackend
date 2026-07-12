namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Entities;

public class StatusRepository : IStatusRepository
{
    private readonly ApplicationDbContext _context;

    public StatusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Status status, CancellationToken cancellationToken)
    {
        _context.Statuses.Add(status);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var status = await _context.Statuses.FindAsync(new object[] { id }, cancellationToken);
        if (status != null)
        {
            _context.Statuses.Remove(status);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Status>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await _context.Statuses
            .OrderBy(s => s.Id.ToString())
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Status?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Statuses.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Status?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.Statuses.FirstOrDefaultAsync(
            s => s.Name.ToUpper() == normalizedName || s.Slug.ToUpper() == normalizedName,
            cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Statuses.CountAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
