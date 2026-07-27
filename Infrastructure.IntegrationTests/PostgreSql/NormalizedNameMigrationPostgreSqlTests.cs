namespace Infrastructure.IntegrationTests.PostgreSql;

using System.Text.Json;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

[Collection(PostgreSqlCollection.CollectionName)]
public sealed class NormalizedNameMigrationPostgreSqlTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string PreviousMigration = "20260722220000_ReconcileStoredCoverStatus";
    private static readonly Guid OwnerId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StatusId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync(PreviousMigration);
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_ShouldCompactKeysAndMergeCollisionsWithoutLosingRelations()
    {
        await SeedCollidingDataAsync();

        await using (var migrationContext = fixture.CreateContext(OwnerId))
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var verify = fixture.CreateContext(OwnerId);
        var book = await verify.Books
            .Include(item => item.Author)
            .Include(item => item.Titles)
            .Include(item => item.BookGenres)
            .Include(item => item.BookTags)
            .Include(item => item.Links)
            .Include(item => item.ProgressHistory)
            .Include(item => item.Cover)
            .SingleAsync();
        var author = await verify.Authors.Include(item => item.Names).SingleAsync();
        var tag = await verify.Tags.SingleAsync();
        var genre = await verify.Genres.SingleAsync();
        var snapshot = await verify.PublicBookSnapshots.SingleAsync();
        var user = await verify.Users.SingleAsync();

        Assert.DoesNotContain(' ', book.NormalizedPrimaryTitle);
        Assert.DoesNotContain(' ', author.NormalizedPrimaryName);
        Assert.DoesNotContain(' ', author.Names.Single().NormalizedName);
        Assert.DoesNotContain(' ', tag.NormalizedName);
        Assert.DoesNotContain(' ', genre.NormalizedName);
        Assert.DoesNotContain(' ', snapshot.NormalizedPrimaryTitle);
        Assert.All(book.Titles, title => Assert.DoesNotContain(' ', title.NormalizedTitle));

        Assert.Equal(author.Id, book.AuthorId);
        Assert.Equal(2, book.Titles.Count);
        Assert.Single(book.BookGenres);
        Assert.Single(book.BookTags);
        Assert.Equal(2, book.Links.Count);
        Assert.Equal(2, book.ProgressHistory.Count);
        Assert.Equal(20, book.CurrentChapterNumber);
        Assert.NotNull(book.Cover);

        Assert.Equal(book.Id, snapshot.SourceBookId);
        Assert.Equal(author.Id, snapshot.PublicAuthorId);
        Assert.Equal([tag.Id], JsonSerializer.Deserialize<Guid[]>(snapshot.PublicTagIdsJson)!);
        Assert.Single(await verify.BookShareAuthorPromotions.ToListAsync());
        Assert.Single(await verify.BookShareTagPromotions.ToListAsync());

        var discardedPaths = await verify.StorageCleanupQueueItems
            .Select(item => item.StoragePath)
            .ToListAsync();
        Assert.NotEmpty(discardedPaths);
        Assert.DoesNotContain(discardedPaths, path =>
            path == book.Cover!.StoragePath ||
            path == book.Cover.ThumbnailStoragePath ||
            path == snapshot.CoverStoragePath ||
            path == snapshot.CoverThumbnailStoragePath);

        Assert.Equal("SPACE USER", user.NormalizedUserName);
        Assert.Equal("MAIL BOX@EXAMPLE.COM", user.NormalizedEmail);

        verify.ChangeTracker.Clear();
        verify.Genres.Add(new Genre { Name = "Invalid direct write", NormalizedName = "INVALID KEY" });
        await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
    }

    private async Task SeedCollidingDataAsync()
    {
        await using var context = fixture.CreateContext(OwnerId);
        var user = new User
        {
            Id = OwnerId,
            UserName = "space-user",
            NormalizedUserName = "SPACE USER",
            Email = "mailbox@example.com",
            NormalizedEmail = "MAIL BOX@EXAMPLE.COM"
        };
        var firstTag = new Tag
        {
            Name = "Sci Fi",
            NormalizedName = "SCI FI",
            IsGlobal = true
        };
        var secondTag = new Tag
        {
            Name = "SciFi",
            NormalizedName = "SCIFI",
            IsGlobal = true,
            Description = "Merged tag description"
        };
        var firstGenre = new Genre { Name = "Slice Of Life", NormalizedName = "SLICE OF LIFE" };
        var secondGenre = new Genre
        {
            Name = "SliceOfLife",
            NormalizedName = "SLICEOFLIFE",
            Description = "Merged genre description"
        };
        var firstAuthor = Author("Er Gen", "ER GEN");
        var secondAuthor = Author("ErGen", "ERGEN");
        var firstBook = Book("The Novel", "THE NOVEL", firstAuthor, 10, "first");
        var secondBook = Book("TheNovel", "THENOVEL", secondAuthor, 20, "second");

        firstBook.BookTags.Add(new BookTag { Book = firstBook, Tag = firstTag });
        secondBook.BookTags.Add(new BookTag { Book = secondBook, Tag = secondTag });
        firstBook.BookGenres.Add(new BookGenre { Book = firstBook, Genre = firstGenre });
        secondBook.BookGenres.Add(new BookGenre { Book = secondBook, Genre = secondGenre });
        secondBook.Titles.Add(new BookTitle
        {
            Title = "A Different Alias",
            NormalizedTitle = "A DIFFERENT ALIAS",
            Source = "Test"
        });

        var firstSnapshot = Snapshot(firstBook, firstAuthor, firstTag, "public/first", DateTimeOffset.UtcNow.AddDays(-1));
        var secondSnapshot = Snapshot(secondBook, secondAuthor, secondTag, "public/second", DateTimeOffset.UtcNow);

        context.AddRange(
            user,
            firstBook,
            secondBook,
            firstSnapshot,
            secondSnapshot,
            new BookShareAuthorPromotion { Author = firstAuthor },
            new BookShareAuthorPromotion { Author = secondAuthor },
            new BookShareTagPromotion { Tag = firstTag },
            new BookShareTagPromotion { Tag = secondTag });
        await context.SaveChangesAsync();
    }

    private static Author Author(string name, string normalizedName)
    {
        var author = new Author
        {
            OwnerId = OwnerId,
            IsPublic = true,
            PrimaryName = name,
            NormalizedPrimaryName = normalizedName
        };
        author.Names.Add(new AuthorName
        {
            Author = author,
            Name = name,
            NormalizedName = normalizedName,
            IsPrimary = true,
            Source = "Test"
        });
        return author;
    }

    private static Book Book(
        string title,
        string normalizedTitle,
        Author author,
        decimal currentChapter,
        string storagePrefix)
    {
        var book = new Book
        {
            OwnerId = OwnerId,
            PrimaryTitle = title,
            NormalizedPrimaryTitle = normalizedTitle,
            Author = author,
            ContentTypeId = ContentTypeId,
            StatusId = StatusId,
            CurrentChapterNumber = currentChapter,
            CurrentChapterLabel = currentChapter.ToString(),
            Cover = new BookCover
            {
                Status = BookCoverStatus.Found,
                Source = BookCoverSource.GoogleBooks,
                StoragePath = $"{storagePrefix}/cover.jpg",
                ThumbnailStoragePath = $"{storagePrefix}/cover.thumb.jpg"
            }
        };
        book.Titles.Add(new BookTitle
        {
            Book = book,
            Title = title,
            NormalizedTitle = normalizedTitle,
            IsPrimary = true,
            Source = "Test"
        });
        book.Links.Add(new BookLink
        {
            Book = book,
            Url = $"https://example.com/{storagePrefix}",
            SourceType = "Test"
        });
        book.ProgressHistory.Add(new BookProgressHistory
        {
            Book = book,
            ChapterNumber = currentChapter,
            ChangedAt = DateTimeOffset.UtcNow
        });
        return book;
    }

    private static PublicBookSnapshot Snapshot(
        Book book,
        Author author,
        Tag tag,
        string storagePrefix,
        DateTimeOffset snapshotAt)
    {
        return new PublicBookSnapshot
        {
            SourceBook = book,
            OwnerId = OwnerId,
            PrimaryTitle = book.PrimaryTitle,
            NormalizedPrimaryTitle = book.NormalizedPrimaryTitle,
            AlternativeTitlesJson = "[]",
            AuthorOtherNamesJson = "[]",
            PublicAuthorId = author.Id,
            ContentType = "Novel",
            GenresJson = "[]",
            TagsJson = JsonSerializer.Serialize(new[] { tag.Name }),
            PublicTagIdsJson = JsonSerializer.Serialize(new[] { tag.Id }),
            CoverStoragePath = $"{storagePrefix}/cover.jpg",
            CoverThumbnailStoragePath = $"{storagePrefix}/cover.thumb.jpg",
            SnapshotAt = snapshotAt
        };
    }
}
