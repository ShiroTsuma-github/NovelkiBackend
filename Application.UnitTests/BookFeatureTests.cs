using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Validators;
using Domain.Associations;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Repositories;

namespace Application.UnitTests;

public class BookFeatureTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ContentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StatusId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CreateBook_ShouldStorePrimaryAndAlternativeTitles()
    {
        var fixture = CreateFixture();
        var command = ValidCreateCommand(alternativeTitles: new[] { new BookTitleInput("Everyone Else is a Returnee") });

        await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.NotNull(fixture.BookRepository.LastBook);
        Assert.Equal("Na Bbaego Da Gwihwanja", fixture.BookRepository.LastBook.PrimaryTitle);
        Assert.Contains(fixture.BookRepository.LastBook.Titles, t => t.IsPrimary && t.Title == "Na Bbaego Da Gwihwanja");
        Assert.Contains(fixture.BookRepository.LastBook.Titles, t => !t.IsPrimary && t.Title == "Everyone Else is a Returnee");
    }

    [Fact]
    public async Task CreateBook_ShouldCreateAuthorFromName()
    {
        var fixture = CreateFixture();
        var command = ValidCreateCommand(authorName: "Toika");

        await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Single(fixture.AuthorRepository.Authors);
        Assert.Equal("Toika", fixture.AuthorRepository.Authors[0].PrimaryName);
        Assert.Equal(fixture.AuthorRepository.Authors[0].Id, fixture.BookRepository.LastBook!.AuthorId);
    }

    [Fact]
    public async Task CreateBook_ShouldStoreLinksTagsAndInitialProgressHistory()
    {
        var fixture = CreateFixture();
        var command = ValidCreateCommand(
            tags: new[] { "favorite", "cultivation" },
            links: new[] { new BookLinkInput("https://novelupdates.com/example", "NU", "NovelUpdates", true, true) },
            currentChapterNumber: 348,
            currentChapterLabel: "ex4");
        command = command with { Notes = "Private notes", Comment = null };

        await fixture.Handler.Handle(command, CancellationToken.None);

        var book = fixture.BookRepository.LastBook!;
        Assert.Equal(2, book.BookTags.Count);
        Assert.Single(book.Links);
        Assert.Single(book.ProgressHistory);
        Assert.Equal("Private notes", book.Notes);
        Assert.Null(book.Comment);
        Assert.Equal(348, book.ProgressHistory.First().ChapterNumber);
        Assert.Equal("ex4", book.ProgressHistory.First().ChapterLabel);
    }

    [Fact]
    public async Task CreateBook_ShouldThrowWhenTitleAlreadyExists()
    {
        var fixture = CreateFixture();
        fixture.BookRepository.Seed(new Book
        {
            Id = Guid.NewGuid(),
            OwnerId = OwnerId,
            PrimaryTitle = "Na Bbaego Da Gwihwanja",
            NormalizedPrimaryTitle = "NA BBAEGO DA GWIHWANJA",
            ContentTypeId = ContentTypeId,
            StatusId = StatusId
        });
        var command = ValidCreateCommand();

        await Assert.ThrowsAsync<EntityAlreadyExistsException<Book, Guid>>(
            () => fixture.Handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateBook_ShouldReplaceEditableDetailsAndAppendProgressHistory()
    {
        var fixture = CreateFixture();
        var book = new Book
        {
            Id = Guid.NewGuid(),
            OwnerId = OwnerId,
            PrimaryTitle = "Old Title",
            NormalizedPrimaryTitle = "OLD TITLE",
            ContentTypeId = ContentTypeId,
            StatusId = StatusId,
            CurrentChapterNumber = 1,
            CurrentChapterLabel = "1"
        };
        book.Titles.Add("Old Title".ToPrimaryTitle());
        book.Links.Add(new BookLink { Url = "https://old.example.com", SourceType = "Other" });
        fixture.BookRepository.Seed(book);
        var handler = new UpdateBookHandler(
            fixture.BookRepository,
            fixture.AuthorRepository,
            new FakeTypeRepository(),
            new FakeStatusRepository(),
            new FakeGenreRepository(),
            new FakeTagRepository(),
            new FakeUser());
        var command = new UpdateBookCommand(
            book.Id,
            "New Title",
            ContentTypeId,
            StatusId,
            null,
            "Toika",
            new[] { new BookTitleInput("New Alias") },
            null,
            new[] { "favorite" },
            100,
            20,
            "20",
            8,
            2,
            "Description",
            "Progress changed",
            "Notes",
            null,
            new[] { new BookLinkInput("https://new.example.com", "New", "Other", true, true) });

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("New Title", book.PrimaryTitle);
        Assert.Contains(book.Titles, t => t.IsPrimary && t.Title == "New Title");
        Assert.Contains(book.Titles, t => !t.IsPrimary && t.Title == "New Alias");
        Assert.Single(book.Links);
        Assert.Equal("https://new.example.com", book.Links.First().Url);
        Assert.Single(book.BookTags);
        Assert.Single(book.ProgressHistory);
        Assert.Equal(20, book.ProgressHistory.First().ChapterNumber);
        Assert.True(fixture.BookRepository.Saved);
    }

    [Fact]
    public void CreateBookValidator_ShouldRejectInvalidFrontendInput()
    {
        var command = ValidCreateCommand(
            links: new[] { new BookLinkInput("not-a-url") },
            currentChapterNumber: 11);
        command = command with { TotalChapters = 10, Rating = 11, Priority = 0 };
        var validator = new CreateBookCommandValidator();

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CurrentChapterNumber");
        Assert.Contains(result.Errors, e => e.PropertyName == "Rating");
        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
        Assert.Contains(result.Errors, e => e.PropertyName == "Links[0].Url");
    }

    private static Fixture CreateFixture()
    {
        var bookRepository = new FakeBookRepository();
        var authorRepository = new FakeAuthorRepository();
        var typeRepository = new FakeTypeRepository();
        var statusRepository = new FakeStatusRepository();
        var genreRepository = new FakeGenreRepository();
        var tagRepository = new FakeTagRepository();
        var user = new FakeUser();
        var handler = new CreateBookHandler(
            bookRepository,
            authorRepository,
            typeRepository,
            statusRepository,
            genreRepository,
            tagRepository,
            user);
        return new Fixture(bookRepository, authorRepository, handler);
    }

    private static CreateBookCommand ValidCreateCommand(
        IEnumerable<BookTitleInput>? alternativeTitles = null,
        string? authorName = null,
        IEnumerable<string>? tags = null,
        IEnumerable<BookLinkInput>? links = null,
        decimal? currentChapterNumber = null,
        string? currentChapterLabel = null)
    {
        return new CreateBookCommand(
            PrimaryTitle: "Na Bbaego Da Gwihwanja",
            ContentTypeId: ContentTypeId,
            StatusId: StatusId,
            AuthorId: null,
            AuthorName: authorName,
            AlternativeTitles: alternativeTitles,
            GenreIds: null,
            Tags: tags,
            TotalChapters: null,
            CurrentChapterNumber: currentChapterNumber,
            CurrentChapterLabel: currentChapterLabel,
            Rating: 9,
            Priority: 1,
            Description: null,
            Comment: null,
            Notes: null,
            RawImportedLine: null,
            Links: links);
    }

    private sealed record Fixture(FakeBookRepository BookRepository, FakeAuthorRepository AuthorRepository, CreateBookHandler Handler);

    private sealed class FakeUser : IUser
    {
        public Guid? Id => OwnerId;
        public Guid RequiredId => OwnerId;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        public Book? LastBook { get; private set; }
        public bool Saved { get; private set; }

        public void Seed(Book book)
        {
            LastBook = book;
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken)
        {
            LastBook = book;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(LastBook?.Id == id && LastBook.OwnerId == ownerId ? LastBook : null);
        public Task<Book?> GetForUpdateAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => GetByIdAsync(id, ownerId, cancellationToken);
        public Task<Book?> GetByNameAsync(string name, Guid ownerId, CancellationToken cancellationToken)
        {
            var normalized = name.Trim().ToUpperInvariant();
            var match = LastBook != null &&
                        LastBook.OwnerId == ownerId &&
                        (LastBook.NormalizedPrimaryTitle == normalized ||
                         LastBook.Titles.Any(t => t.NormalizedTitle == normalized));
            return Task.FromResult(match ? LastBook : null);
        }
        public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(LastBook == null ? 0 : 1);
        public Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task ReplaceEditableCollectionsAsync(
            Guid bookId,
            IEnumerable<BookTitle> titles,
            IEnumerable<BookLink> links,
            IEnumerable<Guid> genreIds,
            IEnumerable<Guid> tagIds,
            BookProgressHistory? progressHistory,
            CancellationToken cancellationToken)
        {
            if (LastBook == null || LastBook.Id != bookId)
            {
                return Task.CompletedTask;
            }

            LastBook.Titles.Clear();
            foreach (var title in titles)
            {
                title.BookId = bookId;
                LastBook.Titles.Add(title);
            }

            LastBook.Links.Clear();
            foreach (var link in links)
            {
                link.BookId = bookId;
                LastBook.Links.Add(link);
            }

            LastBook.BookGenres.Clear();
            foreach (var genreId in genreIds.Distinct())
            {
                LastBook.BookGenres.Add(new BookGenre { BookId = bookId, GenreId = genreId });
            }

            LastBook.BookTags.Clear();
            foreach (var tagId in tagIds.Distinct())
            {
                LastBook.BookTags.Add(new BookTag { BookId = bookId, TagId = tagId });
            }

            if (progressHistory != null)
            {
                progressHistory.BookId = bookId;
                LastBook.ProgressHistory.Add(progressHistory);
            }

            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthorRepository : IAuthorRepository
    {
        public List<Author> Authors { get; } = new();

        public Task AddAsync(Author author, CancellationToken cancellationToken)
        {
            Authors.Add(author);
            return Task.CompletedTask;
        }

        public Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Authors.FirstOrDefault(a => a.Id == id));
        public Task<Author?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Authors.FirstOrDefault(a => a.NormalizedPrimaryName == name.Trim().ToUpperInvariant()));
        public Task<IEnumerable<Author>> SearchAsync(string? search, int take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Author>>(Authors.Take(take));
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTypeRepository : ITypeRepository
    {
        private readonly ContentType _type = new() { Id = ContentTypeId, Name = "Novel", Slug = "novel" };
        public Task AddAsync(ContentType type, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IEnumerable<ContentType>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<ContentType>>(new[] { _type });
        public Task<ContentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ContentType?>(id == ContentTypeId ? _type : null);
        public Task<ContentType?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<ContentType?>(_type);
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeStatusRepository : IStatusRepository
    {
        private readonly Status _status = new() { Id = StatusId, Name = "Reading", Slug = "reading" };
        public Task AddAsync(Status status, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IEnumerable<Status>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Status>>(new[] { _status });
        public Task<Status?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Status?>(id == StatusId ? _status : null);
        public Task<Status?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Status?>(_status);
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGenreRepository : IGenreRepository
    {
        public Task AddAsync(Genre genre, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IEnumerable<Genre>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Genre>>(Array.Empty<Genre>());
        public Task<Genre?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Genre?>(null);
        public Task<IEnumerable<Genre>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Genre>>(Array.Empty<Genre>());
        public Task<Genre?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Genre?>(null);
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagRepository : ITagRepository
    {
        private readonly List<Tag> _tags = new();

        public Task AddAsync(Tag tag, CancellationToken cancellationToken)
        {
            _tags.Add(tag);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Tag>> GetByNamesAsync(Guid ownerId, IEnumerable<string> names, CancellationToken cancellationToken)
        {
            var normalized = names.Select(n => n.Trim().ToUpperInvariant()).ToList();
            return Task.FromResult<IEnumerable<Tag>>(_tags.Where(t => t.OwnerId == ownerId && normalized.Contains(t.NormalizedName)).ToList());
        }

        public Task<Tag?> GetByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken) => Task.FromResult(_tags.FirstOrDefault(t => t.OwnerId == ownerId && t.NormalizedName == name.Trim().ToUpperInvariant()));
        public Task<IEnumerable<Tag>> SearchAsync(Guid ownerId, string? search, int take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Tag>>(_tags.Take(take));
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
