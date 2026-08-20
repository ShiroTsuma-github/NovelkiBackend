namespace Application.Features.AccountFeatures;

using Common.DTOs.User;

public sealed record GetReadingTimeSettingsQuery : IRequest<IReadOnlyCollection<ReadingTimeSettingDto>>;

public sealed class GetReadingTimeSettingsHandler(
    IReadingTimeSettingRepository repository,
    IUser user) : IRequestHandler<GetReadingTimeSettingsQuery, IReadOnlyCollection<ReadingTimeSettingDto>>
{
    public async Task<IReadOnlyCollection<ReadingTimeSettingDto>> Handle(
        GetReadingTimeSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await repository.GetByUserIdAsync(user.RequiredId, cancellationToken);
        return settings
            .Select(setting => new ReadingTimeSettingDto(
                setting.ContentType.Name,
                setting.MinutesPerChapter))
            .ToArray();
    }
}

public sealed record UpdateReadingTimeSettingsCommand(IReadOnlyCollection<ReadingTimeSettingInput> Settings)
    : IRequest<IReadOnlyCollection<ReadingTimeSettingDto>>;

public sealed class UpdateReadingTimeSettingsHandler(
    IReadingTimeSettingRepository repository,
    ITypeRepository typeRepository,
    IUser user) : IRequestHandler<UpdateReadingTimeSettingsCommand, IReadOnlyCollection<ReadingTimeSettingDto>>
{
    public async Task<IReadOnlyCollection<ReadingTimeSettingDto>> Handle(
        UpdateReadingTimeSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Settings.Count > 100 || request.Settings.Any(setting =>
                string.IsNullOrWhiteSpace(setting.ContentType) ||
                setting.MinutesPerChapter < 0 ||
                setting.MinutesPerChapter > 1440) ||
            request.Settings.Select(setting => setting.ContentType.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Settings.Count)
        {
            throw new ValidationException("Reading-time settings must use unique content types and values from 0 to 1440.");
        }

        var minutesByContentType = new Dictionary<Guid, decimal>();
        foreach (var setting in request.Settings)
        {
            var contentType = await typeRepository.GetByNameAsync(setting.ContentType, cancellationToken)
                ?? throw new EntityNotFoundException<ContentType, string>(setting.ContentType);
            minutesByContentType.Add(contentType.Id, setting.MinutesPerChapter);
        }

        await repository.UpsertAsync(
            user.RequiredId,
            minutesByContentType,
            cancellationToken);

        var settings = await repository.GetByUserIdAsync(user.RequiredId, cancellationToken);
        return settings
            .Select(setting => new ReadingTimeSettingDto(
                setting.ContentType.Name,
                setting.MinutesPerChapter))
            .ToArray();
    }
}
