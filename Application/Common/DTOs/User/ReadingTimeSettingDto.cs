namespace Application.Common.DTOs.User;

public sealed record ReadingTimeSettingDto(string ContentType, decimal MinutesPerChapter);

public sealed record UpdateReadingTimeSettingsRequest(IReadOnlyCollection<ReadingTimeSettingInput> Settings);

public sealed record ReadingTimeSettingInput(string ContentType, decimal MinutesPerChapter);
