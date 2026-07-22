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
    public required List<string> AuthorOtherNames { get; init; }
    public required string ContentType { get; init; }
    public required string Status { get; init; }
    public decimal? CurrentChapterNumber { get; init; }
    public string? CurrentChapterLabel { get; init; }
    public decimal? TotalChapters { get; init; }
    public int? Rating { get; init; }
    public int? Priority { get; init; }
    public string? Notes { get; init; }
    public required List<string> Genres { get; init; }
    public required List<string?> GenreDescriptions { get; init; }
    public int GenresCount { get; init; }
    public required List<string> Tags { get; init; }
    public required List<string?> TagDescriptions { get; init; }
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
        return MapListFields(projection,
            new BookListItemDto
            {
                PrimaryTitle = projection.PrimaryTitle,
                ContentType = projection.ContentType,
                Status = projection.Status
            });
    }

    public static AdminBookListItemDto MapAdminListProjection(
        BookListProjection projection,
        IReadOnlyDictionary<Guid, BookOwnerProjection> owners)
    {
        owners.TryGetValue(projection.OwnerId, out var owner);
        return MapListFields(projection,
            new AdminBookListItemDto
            {
                PrimaryTitle = projection.PrimaryTitle,
                ContentType = projection.ContentType,
                Status = projection.Status,
                OwnerId = projection.OwnerId,
                OwnerUsername = owner?.Username,
                OwnerEmail = owner?.Email
            });
    }

    private static BookCoverSummaryDto? MapCoverProjection(BookListProjection projection)
    {
        if (!projection.CoverStatus.HasValue)
        {
            return null;
        }

        var version = projection.CoverLastAttemptAt
                      ?? projection.CoverLastModified
                      ?? projection.CoverCreated
                      ?? DateTimeOffset.UnixEpoch;

        return new BookCoverSummaryDto
        {
            Status = projection.CoverStatus.Value.ToString(),
            Source = projection.CoverSource?.ToString(),
            FailureReason = projection.CoverFailureReason,
            LastAttemptAt = projection.CoverLastAttemptAt,
            ImageUrl =
                projection.HasCoverStoragePath
                    ? ApiRoutes.BookCoverFile(projection.Id, version.ToUnixTimeMilliseconds())
                    : null,
            ThumbnailImageUrl = projection.HasCoverThumbnailStoragePath
                ? ApiRoutes.BookCoverThumbnail(projection.Id, version.ToUnixTimeMilliseconds())
                : null
        };
    }

    private static TListItemDto MapListFields<TListItemDto>(BookListProjection projection, TListItemDto destination)
        where TListItemDto : BookListItemDto
    {
        destination.Id = projection.Id;
        destination.Created = projection.Created;
        destination.LastModified = projection.LastModified;
        destination.PrimaryTitle = projection.PrimaryTitle;
        destination.Description = projection.Description;
        destination.AlternativeTitles = projection.AlternativeTitles;
        destination.AlternativeTitlesCount = projection.AlternativeTitlesCount;
        destination.Author = projection.Author;
        destination.AuthorOtherNames = projection.AuthorOtherNames;
        destination.ContentType = projection.ContentType;
        destination.Status = projection.Status;
        destination.CurrentChapterNumber = projection.CurrentChapterNumber;
        destination.CurrentChapterLabel = projection.CurrentChapterLabel;
        destination.TotalChapters = projection.TotalChapters;
        destination.Rating = projection.Rating;
        destination.Priority = projection.Priority;
        destination.Notes = projection.Notes;
        destination.Genres = projection.Genres;
        destination.GenreDescriptions = projection.Genres
            .Zip(projection.GenreDescriptions)
            .ToDictionary(item => item.First, item => item.Second);
        destination.GenresCount = projection.GenresCount;
        destination.Tags = projection.Tags;
        destination.TagDescriptions = projection.Tags
            .Zip(projection.TagDescriptions)
            .ToDictionary(item => item.First, item => item.Second);
        destination.TagsCount = projection.TagsCount;
        destination.LinksCount = projection.LinksCount;
        destination.Cover = MapCoverProjection(projection);
        return destination;
    }
}
