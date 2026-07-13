namespace Infrastructure.Persistence;

using Application.Common.DTOs.Book;

internal sealed class BookListProjection
{
    public Guid Id { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public Guid OwnerId { get; init; }
    public required string PrimaryTitle { get; init; }
    public string? Description { get; init; }
    public required List<string> AlternativeTitles { get; init; }
    public int AlternativeTitlesCount { get; init; }
    public string? Author { get; init; }
    public required string ContentType { get; init; }
    public required string Status { get; init; }
    public decimal? CurrentChapterNumber { get; init; }
    public string? CurrentChapterLabel { get; init; }
    public decimal? TotalChapters { get; init; }
    public int? Rating { get; init; }
    public int? Priority { get; init; }
    public string? Notes { get; init; }
    public required List<string> Genres { get; init; }
    public int GenresCount { get; init; }
    public required List<string> Tags { get; init; }
    public int TagsCount { get; init; }
    public int LinksCount { get; init; }
    public BookCoverStatus? CoverStatus { get; init; }
    public BookCoverSource? CoverSource { get; init; }
    public string? CoverFailureReason { get; init; }
    public DateTimeOffset? CoverLastAttemptAt { get; init; }
    public DateTimeOffset? CoverLastModified { get; init; }
    public DateTimeOffset? CoverCreated { get; init; }
    public bool HasCoverStoragePath { get; init; }
    public bool HasCoverThumbnailStoragePath { get; init; }
}

internal sealed class BookOwnerProjection
{
    public Guid Id { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
}

internal static class BookListProjectionMapper
{
    public static BookListItemDto MapListProjection(BookListProjection projection)
    {
        return new BookListItemDto
        {
            Id = projection.Id,
            Created = projection.Created,
            LastModified = projection.LastModified,
            PrimaryTitle = projection.PrimaryTitle,
            Description = projection.Description,
            AlternativeTitles = projection.AlternativeTitles,
            AlternativeTitlesCount = projection.AlternativeTitlesCount,
            Author = projection.Author,
            ContentType = projection.ContentType,
            Status = projection.Status,
            CurrentChapterNumber = projection.CurrentChapterNumber,
            CurrentChapterLabel = projection.CurrentChapterLabel,
            TotalChapters = projection.TotalChapters,
            Rating = projection.Rating,
            Priority = projection.Priority,
            Notes = projection.Notes,
            Genres = projection.Genres,
            GenresCount = projection.GenresCount,
            Tags = projection.Tags,
            TagsCount = projection.TagsCount,
            LinksCount = projection.LinksCount,
            Cover = MapCoverProjection(projection)
        };
    }

    public static AdminBookListItemDto MapAdminListProjection(
        BookListProjection projection,
        IReadOnlyDictionary<Guid, BookOwnerProjection> owners)
    {
        var dto = MapListProjection(projection);
        owners.TryGetValue(projection.OwnerId, out var owner);
        return new AdminBookListItemDto
        {
            Id = dto.Id,
            Created = dto.Created,
            LastModified = dto.LastModified,
            PrimaryTitle = dto.PrimaryTitle,
            Description = dto.Description,
            AlternativeTitles = dto.AlternativeTitles,
            AlternativeTitlesCount = dto.AlternativeTitlesCount,
            Author = dto.Author,
            ContentType = dto.ContentType,
            Status = dto.Status,
            CurrentChapterNumber = dto.CurrentChapterNumber,
            CurrentChapterLabel = dto.CurrentChapterLabel,
            TotalChapters = dto.TotalChapters,
            Rating = dto.Rating,
            Priority = dto.Priority,
            Notes = dto.Notes,
            Cover = dto.Cover,
            Genres = dto.Genres,
            GenresCount = dto.GenresCount,
            Tags = dto.Tags,
            TagsCount = dto.TagsCount,
            LinksCount = dto.LinksCount,
            OwnerId = projection.OwnerId,
            OwnerUsername = owner?.Username,
            OwnerEmail = owner?.Email
        };
    }

    private static BookCoverDto? MapCoverProjection(BookListProjection projection)
    {
        if (!projection.CoverStatus.HasValue)
        {
            return null;
        }

        var version = projection.CoverLastAttemptAt
            ?? projection.CoverLastModified
            ?? projection.CoverCreated
            ?? DateTimeOffset.UnixEpoch;

        return new BookCoverDto
        {
            Status = projection.CoverStatus.Value.ToString(),
            Source = projection.CoverSource?.ToString(),
            FailureReason = projection.CoverFailureReason,
            LastAttemptAt = projection.CoverLastAttemptAt,
            ImageUrl = projection.HasCoverStoragePath ? $"/api/v1/book/{projection.Id}/cover/file?v={version.ToUnixTimeMilliseconds()}" : null,
            ThumbnailImageUrl = projection.HasCoverThumbnailStoragePath ? $"/api/v1/book/{projection.Id}/cover/thumbnail?v={version.ToUnixTimeMilliseconds()}" : null
        };
    }
}
