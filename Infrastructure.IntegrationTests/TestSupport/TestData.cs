using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Contexts;

namespace Infrastructure.IntegrationTests.TestSupport;

public static class TestData
{
    public static readonly Guid NovelTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ReadingStatusId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static Author Author(string name, Guid? ownerId = null, bool isPublic = true)
    {
        var author = new Author
        {
            OwnerId = ownerId,
            IsPublic = isPublic,
            PrimaryName = name,
            NormalizedPrimaryName = MappingExtensions.NormalizeName(name)
        };
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

    public static void MakePublishable(Book book)
    {
        var suffix = book.Id.ToString("N");
        book.Description ??= "Publishable test description";
        if (book.Author is null)
        {
            book.Author = Author($"Test Author {suffix}", book.OwnerId, false);
            book.AuthorId = book.Author.Id;
        }
        if (book.BookGenres.Count == 0)
        {
            var genre = Genre($"Test Genre {suffix}");
            book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        }
        if (book.BookTags.Count == 0)
        {
            var tag = Tag(book.OwnerId, $"test-tag-{suffix}");
            book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        }

        book.Cover ??= new BookCover
        {
            Book = book,
            BookId = book.Id,
            StoragePath = $"source/{suffix}.jpg",
            MimeType = "image/jpeg"
        };
        book.Cover.Status = BookCoverStatus.Found;
        book.Cover.StoragePath ??= $"source/{suffix}.jpg";
    }

    public static async Task<Book> AddBookAsync(ApplicationDbContext context, Guid ownerId, string title,
        Author? author = null)
    {
        var book = Book(ownerId, title, author);
        context.Books.Add(book);
        await context.SaveChangesAsync();
        return book;
    }

    public static async Task<Book> AddBookWithRelationsAsync(ApplicationDbContext context, Guid ownerId)
    {
        var author = Author("Toika");
        var genre = Genre("Fantasy");
        var tag = Tag(ownerId, "favorite");
        context.Authors.Add(author);
        context.Genres.Add(genre);
        context.Tags.Add(tag);

        var book = Book(ownerId, "Everyone Else is a Returnee", author);
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
