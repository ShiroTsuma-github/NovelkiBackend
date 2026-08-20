namespace Domain.Repositories;

public interface IReadingTimeSettingRepository
{
    Task<IReadOnlyCollection<ReadingTimeSetting>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task UpsertAsync(
        Guid userId,
        IReadOnlyDictionary<Guid, decimal> minutesByContentType,
        CancellationToken cancellationToken);
}
