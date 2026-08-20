namespace Infrastructure.Persistence;

public sealed class ReadingTimeSettingRepository(ApplicationDbContext context) : IReadingTimeSettingRepository
{
    public async Task<IReadOnlyCollection<ReadingTimeSetting>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.Set<ReadingTimeSetting>()
            .AsNoTracking()
            .Include(setting => setting.ContentType)
            .Where(setting => setting.UserId == userId)
            .OrderBy(setting => setting.ContentType.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        Guid userId,
        IReadOnlyDictionary<Guid, decimal> minutesByContentType,
        CancellationToken cancellationToken)
    {
        if (minutesByContentType.Count == 0)
        {
            return;
        }

        var contentTypeIds = minutesByContentType.Keys.ToArray();
        var existing = await context.Set<ReadingTimeSetting>()
            .Where(setting => setting.UserId == userId && contentTypeIds.Contains(setting.ContentTypeId))
            .ToDictionaryAsync(setting => setting.ContentTypeId, cancellationToken);

        foreach (var (contentTypeId, minutesPerChapter) in minutesByContentType)
        {
            if (existing.TryGetValue(contentTypeId, out var setting))
            {
                setting.MinutesPerChapter = minutesPerChapter;
            }
            else
            {
                context.Set<ReadingTimeSetting>().Add(new ReadingTimeSetting
                {
                    UserId = userId,
                    ContentTypeId = contentTypeId,
                    MinutesPerChapter = minutesPerChapter
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
