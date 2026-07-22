using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Domain.Models;

namespace Application.UnitTests;

using Common.DTOs.Author;
using Common.DTOs.Book;

public class MappingExtensionTests
{
    [Fact]
    public void AnalyticsToDto_ShouldExposeSharesAsPercentages()
    {
        var snapshot = new BookAnalyticsSnapshot(
            DateTimeOffset.UtcNow,
            new BookAnalyticsScopeSnapshot(null, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "month"),
            new BookAnalyticsOverviewSnapshot(174, 0, 174, null, 0, 0, 174),
            new BookAnalyticsCompositionSnapshot([], [new BookAnalyticsRelationCountSnapshot("Fantasy", 87, 0.5)], []),
            BookAnalyticsRatingsSnapshot.Empty,
            BookAnalyticsPlanningSnapshot.Empty,
            BookAnalyticsProgressSnapshot.Empty,
            BookAnalyticsActivitySnapshot.Empty,
            BookAnalyticsLibraryGrowthSnapshot.Empty,
            new BookAnalyticsQualitySnapshot(
                [new BookAnalyticsFieldCompletenessSnapshot("author", 174, 1)],
                [new BookAnalyticsLinkSourceSnapshot("Manual", 87, 87, 0.5)],
                [new BookAnalyticsCoverStatusSnapshot("Found", 87, 0.5)],
                [new BookAnalyticsCoverSourceSnapshot("Manual", 87, 0.5)]));

        var dto = snapshot.ToDto();

        Assert.Equal(100, dto.Quality.FieldCompleteness.Single().ShareOfBooks);
        Assert.Equal(50, dto.Composition.Genres.Single().ShareOfBooks);
        Assert.Equal(50, dto.Quality.LinkSources.Single().ShareOfBooks);
        Assert.Equal(50, dto.Quality.CoverStatuses.Single().ShareOfBooks);
        Assert.Equal(50, dto.Quality.CoverSources.Single().ShareOfBooks);
    }

    [Fact]
    public void NormalizeName_ShouldTrimCollapseWhitespaceAndUppercase()
    {
        var result = MappingExtensions.NormalizeName("  Er\t \n Gen   ");

        Assert.Equal("ER GEN", result);
    }

    [Fact]
    public void CollapseWhitespace_ShouldPreserveDisplayCasing()
    {
        var result = MappingExtensions.CollapseWhitespace("  Lord   of\tMysteries  ");

        Assert.Equal("Lord of Mysteries", result);
    }

    [Fact]
    public void BookToDto_ShouldMapDetails()
    {
        var coverVersion = DateTimeOffset.Parse("2026-07-06T10:00:00Z");
        var author = new Author { PrimaryName = "Toika", NormalizedPrimaryName = "TOIKA" };
        var genre = new Genre
        {
            Name = "Fantasy", NormalizedName = "FANTASY", Description = "Magic and supernatural stories."
        };
        var tag = new Tag
        {
            OwnerId = Guid.NewGuid(),
            Name = "favorite",
            NormalizedName = "FAVORITE",
            Description = "A personal favorite."
        };
        var book = new Book
        {
            Created = DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
            LastModified = DateTimeOffset.Parse("2026-07-02T10:00:00Z"),
            PrimaryTitle = "Everyone Else is a Returnee",
            NormalizedPrimaryTitle = "EVERYONE ELSE IS A RETURNEE",
            Description = "Portal fantasy with returnee progression.",
            Author = author,
            AuthorId = author.Id,
            ContentType = new ContentType { Name = "Novel", Slug = "novel" },
            Status = new Status { Name = "Reading", Slug = "reading" },
            OwnerId = Guid.NewGuid(),
            CurrentChapterNumber = 348,
            CurrentChapterLabel = "ex4",
            Rating = 9,
            Priority = 1
        };
        book.Titles.Add("Everyone Else is a Returnee".ToPrimaryTitle());
        book.Titles.Add(new BookTitle { Title = "Na Bbaego Da Gwihwanja", NormalizedTitle = "NA BBAEGO DA GWIHWANJA" });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        book.Links.Add(new BookLink { Url = "https://example.com", SourceType = "NovelUpdates", IsPrimary = true });
        book.ProgressHistory.Add(new BookProgressHistory
        {
            ChangedAt = DateTimeOffset.Parse("2026-07-03T10:00:00Z"),
            ChapterNumber = 348,
            ChapterLabel = "ex4",
            Comment = "Progress note"
        });
        book.Cover = new BookCover
        {
            Created = DateTimeOffset.Parse("2026-07-04T10:00:00Z"),
            LastModified = DateTimeOffset.Parse("2026-07-05T10:00:00Z"),
            Status = BookCoverStatus.Found,
            Source = BookCoverSource.Jikan,
            StoragePath = "owner/book.jpg",
            ThumbnailStoragePath = "owner/book.thumb.jpg",
            OriginalImageUrl = "https://cdn.example.com/book.jpg",
            MimeType = "image/jpeg",
            ThumbnailMimeType = "image/jpeg",
            SizeBytes = 42,
            ThumbnailSizeBytes = 21,
            LastAttemptAt = coverVersion
        };

        var dto = book.ToDto();

        Assert.Equal("Everyone Else is a Returnee", dto.PrimaryTitle);
        Assert.Equal("Portal fantasy with returnee progression.", dto.Description);
        Assert.Equal(DateTimeOffset.Parse("2026-07-01T10:00:00Z"), dto.Created);
        Assert.Equal(DateTimeOffset.Parse("2026-07-02T10:00:00Z"), dto.LastModified);
        Assert.Contains("Na Bbaego Da Gwihwanja", dto.AlternativeTitles);
        Assert.Equal("Toika", dto.Author);
        Assert.Equal("Novel", dto.ContentType);
        Assert.Equal("Reading", dto.Status);
        Assert.Contains("Fantasy", dto.Genres);
        Assert.Equal("Magic and supernatural stories.", dto.GenreDescriptions["Fantasy"]);
        Assert.Contains("favorite", dto.Tags);
        Assert.Equal("A personal favorite.", dto.TagDescriptions["favorite"]);
        Assert.Single(dto.Links);
        var progressEntry = Assert.Single(dto.ProgressHistory);
        Assert.Equal("Progress note", progressEntry.Comment);
        Assert.NotNull(dto.Cover);
        Assert.Equal("Found", dto.Cover.Status);
        Assert.Equal("Jikan", dto.Cover.Source);
        Assert.Equal($"/api/v1/book/{book.Id}/cover/file?v={coverVersion.ToUnixTimeMilliseconds()}",
            dto.Cover.ImageUrl);
        Assert.Equal($"/api/v1/book/{book.Id}/cover/thumbnail?v={coverVersion.ToUnixTimeMilliseconds()}",
            dto.Cover.ThumbnailImageUrl);
    }

    [Fact]
    public void AuthorToDto_ShouldMapAliases()
    {
        var author = new Author { PrimaryName = "Er Gen", NormalizedPrimaryName = "ER GEN" };
        author.Names.Add(new AuthorName { Name = "Ergen", NormalizedName = "ERGEN", IsPrimary = false });

        var dto = author.ToDto();

        Assert.Equal("Er Gen", dto.PrimaryName);
        Assert.Contains("Ergen", dto.OtherNames);
    }
}
