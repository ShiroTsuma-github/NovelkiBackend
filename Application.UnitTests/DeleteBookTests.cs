using Application.Common.Interfaces;
using Application.Features.BookFeatures.Commands;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Repositories;
using Moq;

namespace Application.UnitTests;

public class DeleteBookTests
{
    private static readonly Guid OwnerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task DeleteBook_ShouldDeleteStorageFileAndInvalidateCache()
    {
        var book = new Book
        {
            Id = Guid.NewGuid(),
            OwnerId = OwnerId,
            PrimaryTitle = "Novel",
            NormalizedPrimaryTitle = "NOVEL",
            ContentTypeId = Guid.NewGuid(),
            StatusId = Guid.NewGuid(),
            Cover = new BookCover
            {
                StoragePath = "owner/book.jpg",
                ThumbnailStoragePath = "owner/book.thumb.jpg",
                MimeType = "image/jpeg"
            }
        };
        var repository = new FakeBookRepository(book);
        var storage = new FakeBookCoverStorage();
        var cache = new FakeBookListCacheInvalidator();
        var handler = new DeleteBookHandler(repository, storage, cache, new FakeUser());

        await handler.Handle(new DeleteBookCommand(book.Id), CancellationToken.None);

        Assert.Equal(book.Id, repository.DeletedBookId);
        Assert.Equal(["owner/book.jpg", "owner/book.thumb.jpg"], storage.DeletedPaths);
        Assert.Equal(OwnerId, cache.InvalidatedOwnerId);
    }

    [Fact]
    public async Task DeleteBook_ShouldThrowWhenBookDoesNotBelongToUser()
    {
        var handler = new DeleteBookHandler(
            new FakeBookRepository(null),
            new FakeBookCoverStorage(),
            new FakeBookListCacheInvalidator(),
            new FakeUser());

        await Assert.ThrowsAsync<EntityNotFoundException<Book, Guid>>(() =>
            handler.Handle(new DeleteBookCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteBook_ShouldUnlistBeforeDeletingBook()
    {
        var book = CreateBook();
        var calls = new List<string>();
        var repository = new FakeBookRepository(book, calls);
        var publicBooks = new Mock<IPublicBookService>();
        publicBooks.Setup(service => service.UnlistBySourceBookAsync(book.Id, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("unlist"))
            .Returns(Task.CompletedTask);
        var handler = new DeleteBookHandler(repository, new FakeBookCoverStorage(),
            new FakeBookListCacheInvalidator(), new FakeUser(), publicBooks.Object);

        await handler.Handle(new DeleteBookCommand(book.Id), CancellationToken.None);

        Assert.Equal(["unlist", "delete"], calls);
    }

    [Fact]
    public async Task DeleteBook_ShouldStopWhenUnlistFails()
    {
        var book = CreateBook();
        var repository = new FakeBookRepository(book);
        var publicBooks = new Mock<IPublicBookService>();
        publicBooks.Setup(service => service.UnlistBySourceBookAsync(book.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unlist failed"));
        var storage = new FakeBookCoverStorage();
        var handler = new DeleteBookHandler(repository, storage, new FakeBookListCacheInvalidator(),
            new FakeUser(), publicBooks.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteBookCommand(book.Id), CancellationToken.None));

        Assert.Null(repository.DeletedBookId);
        Assert.Empty(storage.DeletedPaths);
    }

    [Fact]
    public async Task DeleteBook_ShouldContinueWhenBookHasNoPublicSnapshot()
    {
        var book = CreateBook();
        var repository = new FakeBookRepository(book);
        var publicBooks = new Mock<IPublicBookService>();
        publicBooks.Setup(service => service.UnlistBySourceBookAsync(book.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new DeleteBookHandler(repository, new FakeBookCoverStorage(),
            new FakeBookListCacheInvalidator(), new FakeUser(), publicBooks.Object);

        await handler.Handle(new DeleteBookCommand(book.Id), CancellationToken.None);

        Assert.Equal(book.Id, repository.DeletedBookId);
        publicBooks.VerifyAll();
    }

    private static Book CreateBook() => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = OwnerId,
        PrimaryTitle = "Novel",
        NormalizedPrimaryTitle = "NOVEL",
        ContentTypeId = Guid.NewGuid(),
        StatusId = Guid.NewGuid()
    };

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
        private readonly Book? _book;

        private readonly List<string>? _calls;

        public FakeBookRepository(Book? book, List<string>? calls = null)
        {
            _book = book;
            _calls = calls;
        }

        public Guid? DeletedBookId { get; private set; }

        public Task AddAsync(Book book, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
        {
            _calls?.Add("delete");
            DeletedBookId = _book?.OwnerId == ownerId ? id : null;
            return Task.CompletedTask;
        }

        public Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_book?.Id == id && _book.OwnerId == ownerId ? _book : null);
        }

        public Task<Book?> GetByNameAsync(string name, Guid ownerId, Guid contentTypeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Book?>(null);
        }

        public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookCoverStorage : IBookCoverStorage
    {
        public List<string> DeletedPaths { get; } = [];

        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName,
            string? contentType, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            if (storagePath is not null)
            {
                DeletedPaths.Add(storagePath);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookListCacheInvalidator : IBookListCacheInvalidator
    {
        public Guid? InvalidatedOwnerId { get; private set; }

        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            InvalidatedOwnerId = ownerId;
            return Task.CompletedTask;
        }
    }
}
