using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Contexts;

namespace Infrastructure.IntegrationTests.TestSupport;

public static class TestData
{
    public static readonly Guid NovelTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ReadingStatusId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static Author Author(string name)
    {
        var author = new Author { PrimaryName = name, NormalizedPrimaryName = MappingExtensions.NormalizeName(name) };
        author.Names.Add(new AuthorName
        {
            Name = name, NormalizedName = MappingExtensions.NormalizeName(name), IsPrimary = true, Source = "Test"
        });
        return author;
    }

    public static Genre Genre(string name)
    {
        return new Genre { Name = name, NormalizedName = MappingExtensions.NormalizeName(name) };
    }

    public static Tag Tag(Guid ownerId, string name)
    {
        return new Tag { OwnerId = ownerId, Name = name, NormalizedName = MappingExtensions.NormalizeName(name) };
    }

    public static Book Book(Guid ownerId, string title, Author? author = null)
    {
        var book = new Book
        {
            OwnerId = ownerId,
            PrimaryTitle = title,
            NormalizedPrimaryTitle = MappingExtensions.NormalizeName(title),
            Author = author,
            AuthorId = author?.Id,
            ContentTypeId = NovelTypeId,
            StatusId = ReadingStatusId
        };
        book.Titles.Add(title.ToPrimaryTitle());
        return book;
    }

    public static async Task<Book> AddBookAsync(ApplicationDbContext context, Guid ownerId, string title,
        Author? author = null)
    {
        Book book = Book(ownerId, title, author);
        context.Books.Add(book);
        await context.SaveChangesAsync();
        return book;
    }

    public static async Task<Book> AddBookWithRelationsAsync(ApplicationDbContext context, Guid ownerId)
    {
        Author author = Author("Toika");
        Genre genre = Genre("Fantasy");
        Tag tag = Tag(ownerId, "favorite");
        context.Authors.Add(author);
        context.Genres.Add(genre);
        context.Tags.Add(tag);

        Book book = Book(ownerId, "Everyone Else is a Returnee", author);
        book.Titles.Add(new BookTitle
        {
            Title = "Na Bbaego Da Gwihwanja",
            NormalizedTitle = MappingExtensions.NormalizeName("Na Bbaego Da Gwihwanja"),
            IsPrimary = false,
            Source = "Test"
        });
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        book.Links.Add(new BookLink { Url = "https://example.com", SourceType = "NovelUpdates", IsPrimary = true });
        book.ProgressHistory.Add(new BookProgressHistory { ChapterNumber = 10, ChapterLabel = "10" });
        book.Cover = new BookCover
        {
            Status = BookCoverStatus.Found,
            Source = BookCoverSource.NovelUpdates,
            StoragePath = "11111111111111111111111111111111/example.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 123
        };
        context.Books.Add(book);
        await context.SaveChangesAsync();
        return book;
    }
}
