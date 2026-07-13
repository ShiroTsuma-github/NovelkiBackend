using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using Infrastructure.BookCovers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace Application.UnitTests;

public class BookCoverProcessorTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BookId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ProcessAsync_ShouldReturnWhenCoverIsMissing()
    {
        var repository = new FakeBookCoverRepository();
        var processor = CreateProcessor(repository);

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReturnWhenCoverIsNotPending()
    {
        var repository = new FakeBookCoverRepository { Cover = Cover(BookCoverStatus.Found) };
        var processor = CreateProcessor(repository);

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkNotFoundWhenProvidersReturnNoCandidate()
    {
        var cover = Cover(BookCoverStatus.Pending);
        var repository = new FakeBookCoverRepository { Cover = cover };
        var processor = CreateProcessor(repository);

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(BookCoverStatus.NotFound, cover.Status);
        Assert.Contains("No cover found", cover.FailureReason);
        Assert.NotNull(cover.LastAttemptAt);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldStoreResolvedCoverAndInvalidateCache()
    {
        var cover = Cover(BookCoverStatus.Pending);
        var repository = new FakeBookCoverRepository { Cover = cover };
        var storage = new FakeBookCoverStorage();
        var cache = new FakeBookListCacheInvalidator();
        var processor = CreateProcessor(
            repository,
            storage: storage,
            cacheInvalidator: cache,
            provider: new FakeBookCoverProvider(new BookCoverCandidate(BookCoverSource.GoogleBooks, "https://cdn.example.com/covers/test.jpg")),
            httpClientFactory: new FakeHttpClientFactory(new ByteArrayContent([1, 2, 3])));

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(BookCoverStatus.Found, cover.Status);
        Assert.Equal(BookCoverSource.GoogleBooks, cover.Source);
        Assert.Equal("original.jpg", cover.StoragePath);
        Assert.Equal("thumb.jpg", cover.ThumbnailStoragePath);
        Assert.Equal("https://cdn.example.com/covers/test.jpg", cover.OriginalImageUrl);
        Assert.Equal("image/jpeg", cover.MimeType);
        Assert.Equal(1, storage.SaveCount);
        Assert.Equal(1, cache.InvalidateCount);
        Assert.Contains(cover.Book.Links, link => link.Url == cover.OriginalImageUrl && link.SourceType == "Cover");
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkFailedAndInvalidateCacheForValidationFailure()
    {
        var cover = Cover(BookCoverStatus.Pending);
        var repository = new FakeBookCoverRepository { Cover = cover };
        var cache = new FakeBookListCacheInvalidator();
        var processor = CreateProcessor(
            repository,
            storage: new FakeBookCoverStorage { SaveException = new ValidationException("invalid image") },
            cacheInvalidator: cache,
            provider: new FakeBookCoverProvider(new BookCoverCandidate(BookCoverSource.OpenLibrary, "https://cdn.example.com/test.png")));

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(BookCoverStatus.Failed, cover.Status);
        Assert.Contains("invalid image", cover.FailureReason);
        Assert.Equal(1, cache.InvalidateCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkNotFoundWithoutCacheInvalidationForProviderResponseFailure()
    {
        var cover = Cover(BookCoverStatus.Pending);
        var repository = new FakeBookCoverRepository { Cover = cover };
        var cache = new FakeBookListCacheInvalidator();
        var processor = CreateProcessor(
            repository,
            cacheInvalidator: cache,
            provider: new FakeBookCoverProvider(new BookCoverCandidate(BookCoverSource.AniList, "https://cdn.example.com/test.png")),
            httpClientFactory: new FakeHttpClientFactory(exception: new JsonException("invalid start of a value")));

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(BookCoverStatus.NotFound, cover.Status);
        Assert.Equal("No valid cover response was found from the configured providers.", cover.FailureReason);
        Assert.Equal(0, cache.InvalidateCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldTruncateUnexpectedFailureReasonAndInvalidateCache()
    {
        var cover = Cover(BookCoverStatus.Pending);
        var repository = new FakeBookCoverRepository { Cover = cover };
        var cache = new FakeBookListCacheInvalidator();
        var processor = CreateProcessor(
            repository,
            storage: new FakeBookCoverStorage { SaveException = new InvalidOperationException(new string('x', 1200)) },
            cacheInvalidator: cache,
            provider: new FakeBookCoverProvider(new BookCoverCandidate(BookCoverSource.Wikidata, "https://cdn.example.com/test.png")));

        await processor.ProcessAsync(BookId, CancellationToken.None);

        Assert.Equal(BookCoverStatus.Failed, cover.Status);
        Assert.Equal(1000, cover.FailureReason!.Length);
        Assert.Equal(1, cache.InvalidateCount);
    }

    private static BookCoverProcessor CreateProcessor(
        FakeBookCoverRepository? repository = null,
        FakeBookCoverStorage? storage = null,
        FakeBookListCacheInvalidator? cacheInvalidator = null,
        FakeBookCoverProvider? provider = null,
        FakeHttpClientFactory? httpClientFactory = null)
    {
        return new BookCoverProcessor(
            repository ?? new FakeBookCoverRepository(),
            storage ?? new FakeBookCoverStorage(),
            new BookCoverResolver([provider ?? new FakeBookCoverProvider(null)]),
            cacheInvalidator ?? new FakeBookListCacheInvalidator(),
            httpClientFactory ?? new FakeHttpClientFactory(new ByteArrayContent([1])),
            NullLogger<BookCoverProcessor>.Instance);
    }

    private static BookCover Cover(BookCoverStatus status)
    {
        var book = new Book
        {
            Id = BookId,
            OwnerId = OwnerId,
            PrimaryTitle = "Test Book",
            NormalizedPrimaryTitle = "test book"
        };

        return new BookCover
        {
            BookId = BookId,
            Book = book,
            Status = status
        };
    }

    private sealed class FakeBookCoverRepository : IBookCoverRepository
    {
        public BookCover? Cover { get; init; }
        public int SaveCount { get; private set; }

        public Task AddAsync(BookCover cover, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(BookCover cover, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Cover);
        }

        public Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Cover);
        }

        public Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<BookCover>>(Cover == null ? [] : [Cover]);
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookCoverStorage : IBookCoverStorage
    {
        public Exception? SaveException { get; init; }
        public int SaveCount { get; private set; }

        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(new MemoryStream([1]));
        }

        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            SaveCount++;
            if (SaveException != null)
            {
                throw SaveException;
            }

            return Task.FromResult(new BookCoverStoredFiles(
                new BookCoverStoredVariant("original.jpg", "image/jpeg", 3, 500, 700),
                new BookCoverStoredVariant("thumb.jpg", "image/webp", 1, 200, 280)));
        }
    }

    private sealed class FakeBookListCacheInvalidator : IBookListCacheInvalidator
    {
        public int InvalidateCount { get; private set; }

        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            InvalidateCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookCoverProvider : IBookCoverProvider
    {
        private readonly BookCoverCandidate? _candidate;

        public FakeBookCoverProvider(BookCoverCandidate? candidate)
        {
            _candidate = candidate;
        }

        public Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
        {
            return Task.FromResult(_candidate);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpContent? _content;
        private readonly Exception? _exception;

        public FakeHttpClientFactory(HttpContent? content = null, Exception? exception = null)
        {
            _content = content;
            _exception = exception;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeHttpMessageHandler(_content, _exception));
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpContent? _content;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(HttpContent? content, Exception? exception)
        {
            _content = content;
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = _content ?? new ByteArrayContent([1])
            });
        }
    }
}
