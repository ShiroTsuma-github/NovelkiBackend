using Application.Common;
using Domain.Associations;
using Domain.Entities;

namespace Application.UnitTests;

public class MappingExtensionTests
{
    [Fact]
    public void BookToDto_ShouldMapDetails()
    {
        var author = new Author { PrimaryName = "Toika", NormalizedPrimaryName = "TOIKA" };
        var genre = new Genre { Name = "Fantasy", NormalizedName = "FANTASY" };
        var tag = new Tag { OwnerId = Guid.NewGuid(), Name = "favorite", NormalizedName = "FAVORITE" };
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
        book.Cover = new BookCover
        {
            Status = BookCoverStatus.Found,
            Source = BookCoverSource.Jikan,
            StoragePath = "owner/book.jpg",
            OriginalImageUrl = "https://cdn.example.com/book.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 42
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
        Assert.Contains("favorite", dto.Tags);
        Assert.Single(dto.Links);
        Assert.NotNull(dto.Cover);
        Assert.Equal("Found", dto.Cover.Status);
        Assert.Equal("Jikan", dto.Cover.Source);
        Assert.Equal($"/api/v1/book/{book.Id}/cover/file", dto.Cover.ImageUrl);
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
