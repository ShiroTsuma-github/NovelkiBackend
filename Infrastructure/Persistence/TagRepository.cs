namespace Infrastructure.Persistence;

public class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        return await _context.Tags
            .Include(t => t.BookTags)
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Id == id, cancellationToken);
    }

    public async Task<Tag?> GetByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken)
    {
        var normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.Tags.FirstOrDefaultAsync(
            t => t.OwnerId == ownerId && t.NormalizedName == normalizedName,
            cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetByNamesAsync(Guid ownerId, IEnumerable<string> names,
        CancellationToken cancellationToken)
    {
        var normalizedNames = names.Select(MappingExtensions.NormalizeName).Distinct().ToList();
        return await _context.Tags
            .Where(t => t.OwnerId == ownerId && normalizedNames.Contains(t.NormalizedName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tag>> SearchAsync(Guid ownerId, string? search, int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Tags.Where(t => t.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = MappingExtensions.NormalizeName(search);
            query = query.Where(t => t.NormalizedName.Contains(normalizedSearch));
        }

        return await query.OrderBy(t => t.Name).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken)
    {
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tag tag, CancellationToken cancellationToken)
    {
        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
