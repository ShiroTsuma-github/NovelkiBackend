using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Features.BookFeatures.Commands;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.BookCovers;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Application.UnitTests;

public class BookCoverTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BookId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Resolver_ShouldUseFirstProviderWithCandidate()
    {
        var resolver = new BookCoverResolver(new IBookCoverProvider[]
        {
            new FakeProvider(null),
            new FakeProvider(new BookCoverCandidate(BookCoverSource.GoogleBooks, "https://example.com/cover.jpg")),
            new FakeProvider(new BookCoverCandidate(BookCoverSource.Jikan, "https://example.com/other.jpg"))
        });

        var result = await resolver.FindAsync(CreateBook(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(BookCoverSource.GoogleBooks, result.Source);
        Assert.Equal("https://example.com/cover.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task BookLinkMetadataProvider_ShouldReadOpenGraphImage()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="/covers/book.jpg" />
              </head>
            </html>
            """;
        var provider = new BookLinkMetadataCoverProvider(new HttpClient(new FakeHttpMessageHandler(html))
        {
            BaseAddress = new Uri("https://source.example")
        });
        var book = CreateBook();
        book.Links.Add(new BookLink { Url = "https://source.example/series/book", SourceType = "Official", IsPrimary = true });

        var result = await provider.FindAsync(book, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(BookCoverSource.BookLinkMetadata, result.Source);
        Assert.Equal("https://source.example/covers/book.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task SetBookCoverFromUrl_ShouldStoreManualUrlCover()
    {
        var book = CreateBook(new BookCover
        {
            BookId = BookId,
            Status = BookCoverStatus.NotFound,
            FailureReason = "No cover found in saved links, AniList, Jikan, Google Books, Open Library, or Wikidata."
        });
        var repository = new FakeBookRepository(book);
        var coverRepository = new FakeBookCoverRepository();
        var remoteImageService = new FakeRemoteImageService();
        var handler = new SetBookCoverFromUrlHandler(
            repository,
            coverRepository,
            new FakeCoverStorage(),
            remoteImageService,
            new FakeBookListCacheInvalidator(),
            new FakeUser());

        var dto = await handler.Handle(new SetBookCoverFromUrlCommand(book.Id, "https://example.com/cover.jpg"), CancellationToken.None);

        Assert.Equal("Uploaded", dto.Status);
        Assert.Equal("ManualUrl", dto.Source);
        Assert.Equal("https://example.com/cover.jpg", book.Cover!.OriginalImageUrl);
        Assert.Equal("owner/book.jpg", book.Cover.StoragePath);
        Assert.Equal("owner/book.thumb.jpg", book.Cover.ThumbnailStoragePath);
        Assert.True(repository.Saved);
        Assert.False(coverRepository.Added);
    }

    [Fact]
    public async Task SetBookCoverFromUrl_ShouldAddCover_WhenBookHasNoCoverRow()
    {
        var book = CreateBook(hasCover: false);
        var repository = new FakeBookRepository(book);
        var coverRepository = new FakeBookCoverRepository();
        var handler = new SetBookCoverFromUrlHandler(
            repository,
            coverRepository,
            new FakeCoverStorage(),
            new FakeRemoteImageService(),
            new FakeBookListCacheInvalidator(),
            new FakeUser());

        var dto = await handler.Handle(new SetBookCoverFromUrlCommand(book.Id, "https://example.com/cover.jpg"), CancellationToken.None);

        Assert.Equal("Uploaded", dto.Status);
        Assert.Equal("ManualUrl", dto.Source);
        Assert.NotNull(book.Cover);
        Assert.True(coverRepository.Added);
        Assert.False(repository.Saved);
    }

    [Fact]
    public async Task RemoteImageService_ShouldAcceptImageBytes_WhenUrlExtensionIsMisleading()
    {
        var storageRoot = CreateTempStorageRoot();
        try
        {
            var pngBytes = CreateTestPngBytes();
            var service = CreateRemoteImageService(storageRoot, pngBytes, "text/plain");

            var stored = await service.SaveFromUrlAsync(OwnerId, BookId, "https://example.com/not-an-image.txt", CancellationToken.None);

            Assert.Equal("image/jpeg", stored.Original.MimeType);
            Assert.EndsWith(".jpg", stored.Original.StoragePath);
            Assert.EndsWith(".thumb.jpg", stored.Thumbnail.StoragePath);
            Assert.True(File.Exists(Path.Combine(storageRoot, stored.Original.StoragePath)));
            Assert.True(File.Exists(Path.Combine(storageRoot, stored.Thumbnail.StoragePath)));
        }
        finally
        {
            DeleteTempStorageRoot(storageRoot);
        }
    }

    [Fact]
    public async Task RemoteImageService_ShouldRejectNonImageBytes_WhenUrlLooksLikeJpg()
    {
        var storageRoot = CreateTempStorageRoot();
        try
        {
            var service = CreateRemoteImageService(storageRoot, Encoding.UTF8.GetBytes("<html>not an image</html>"), "text/html");

            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
                service.SaveFromUrlAsync(OwnerId, BookId, "https://example.com/cover.jpg", CancellationToken.None));
        }
        finally
        {
            DeleteTempStorageRoot(storageRoot);
        }
    }

    [Theory]
    [InlineData("http://127.0.0.1/cover.jpg")]
    [InlineData("http://10.0.0.5/cover.jpg")]
    [InlineData("http://172.16.0.5/cover.jpg")]
    [InlineData("http://192.168.1.5/cover.jpg")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/cover.jpg")]
    public async Task RemoteImageService_ShouldRejectPrivateOrLoopbackHosts(string imageUrl)
    {
        var storageRoot = CreateTempStorageRoot();
        try
        {
            var handler = new FakeHttpMessageHandler(CreateTestPngBytes(), "image/png");
            var service = CreateRemoteImageService(storageRoot, handler);

            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
                service.SaveFromUrlAsync(OwnerId, BookId, imageUrl, CancellationToken.None));

            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            DeleteTempStorageRoot(storageRoot);
        }
    }

    [Fact]
    public async Task RemoteImageService_ShouldRejectRedirectResponses()
    {
        var storageRoot = CreateTempStorageRoot();
        try
        {
            var service = CreateRemoteImageService(
                storageRoot,
                new FakeHttpMessageHandler(Array.Empty<byte>(), "text/plain", HttpStatusCode.Redirect));

            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
                service.SaveFromUrlAsync(OwnerId, BookId, "https://example.com/redirect", CancellationToken.None));
        }
        finally
        {
            DeleteTempStorageRoot(storageRoot);
        }
    }

    [Fact]
    public async Task RefreshBookCover_ShouldAddPendingCover_WhenBookHasNoCoverRow()
    {
        var book = CreateBook(hasCover: false);
        var repository = new FakeBookRepository(book);
        var coverRepository = new FakeBookCoverRepository();
        var queue = new FakeBookCoverQueue();
        var handler = new RefreshBookCoverHandler(
            repository,
            coverRepository,
            queue,
            new FakeCoverStorage(),
            new FakeBookListCacheInvalidator(),
            new FakeUser());

        var dto = await handler.Handle(new RefreshBookCoverCommand(book.Id), CancellationToken.None);

        Assert.Equal("Pending", dto.Status);
        Assert.NotNull(book.Cover);
        Assert.True(coverRepository.Added);
        Assert.False(repository.Saved);
        Assert.Equal(book.Id, queue.QueuedBookId);
    }

    private static Book CreateBook(BookCover? cover = null, bool hasCover = true)
    {
        return new Book
        {
            Id = BookId,
            OwnerId = OwnerId,
            PrimaryTitle = "I Shall Seal the Heavens",
            NormalizedPrimaryTitle = "I SHALL SEAL THE HEAVENS",
            ContentTypeId = Guid.NewGuid(),
            StatusId = Guid.NewGuid(),
            Cover = hasCover ? cover ?? new BookCover { BookId = BookId } : null
        };
    }

    private static BookCoverRemoteImageService CreateRemoteImageService(string storageRoot, byte[] content, string contentType)
    {
        return CreateRemoteImageService(storageRoot, new FakeHttpMessageHandler(content, contentType));
    }

    private static BookCoverRemoteImageService CreateRemoteImageService(string storageRoot, HttpMessageHandler handler)
    {
        var storage = new LocalBookCoverStorage(Options.Create(new BookCoverOptions
        {
            StorageRoot = storageRoot,
            MaxBytes = 1024
        }));
        var client = new HttpClient(handler);

        return new BookCoverRemoteImageService(new FakeHttpClientFactory(client), storage);
    }

    private static byte[] CreateTestPngBytes()
    {
        using var image = new Image<Rgba32>(2, 3, new Rgba32(255, 0, 0, 128));
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    private static string CreateTempStorageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "novelki-cover-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempStorageRoot(string storageRoot)
    {
        if (Directory.Exists(storageRoot))
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private sealed class FakeProvider : IBookCoverProvider
    {
        private readonly BookCoverCandidate? _candidate;

        public FakeProvider(BookCoverCandidate? candidate)
        {
            _candidate = candidate;
        }

        public Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
        {
            return Task.FromResult(_candidate);
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        private readonly string _contentType;
        private readonly HttpStatusCode _statusCode;
        public int RequestCount { get; private set; }

        public FakeHttpMessageHandler(string html)
            : this(Encoding.UTF8.GetBytes(html), "text/html")
        {
        }

        public FakeHttpMessageHandler(byte[] content, string contentType)
            : this(content, contentType, HttpStatusCode.OK)
        {
        }

        public FakeHttpMessageHandler(byte[] content, string contentType, HttpStatusCode statusCode)
        {
            _content = content;
            _contentType = contentType;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var content = new ByteArrayContent(_content);
            content.Headers.ContentType = new MediaTypeHeaderValue(_contentType);

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = content
            });
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

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
        private readonly Book _book;
        public bool Saved { get; private set; }

        public FakeBookRepository(Book book)
        {
            _book = book;
        }

        public Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(id == _book.Id && ownerId == _book.OwnerId ? _book : null);
        }

        public Task<Book?> GetByNameAsync(string name, Guid ownerId, Guid contentTypeId, CancellationToken cancellationToken) => Task.FromResult<Book?>(null);
        public Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(Book book, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookCoverRepository : IBookCoverRepository
    {
        public bool Saved { get; private set; }
        public bool Added { get; private set; }

        public Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken) => Task.FromResult<BookCover?>(null);
        public Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken) => Task.FromResult<BookCover?>(null);
        public Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<BookCover>>(Array.Empty<BookCover>());
        public Task AddAsync(BookCover cover, CancellationToken cancellationToken)
        {
            Added = true;
            Saved = true;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(BookCover cover, CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookListCacheInvalidator : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCoverStorage : IBookCoverStorage
    {
        public Task<BookCoverStoredFiles> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BookCoverStoredFiles(
                new BookCoverStoredVariant("owner/book.jpg", "image/jpeg", 123, 900, 1350),
                new BookCoverStoredVariant("owner/book.thumb.jpg", "image/jpeg", 45, 500, 750)));
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRemoteImageService : IBookCoverRemoteImageService
    {
        public Task<BookCoverStoredFiles> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BookCoverStoredFiles(
                new BookCoverStoredVariant("owner/book.jpg", "image/jpeg", 123, 900, 1350),
                new BookCoverStoredVariant("owner/book.thumb.jpg", "image/jpeg", 45, 500, 750)));
        }
    }

    private sealed class FakeBookCoverQueue : IBookCoverQueue
    {
        public Guid? QueuedBookId { get; private set; }

        public ValueTask QueueAsync(Guid bookId, CancellationToken cancellationToken)
        {
            QueuedBookId = bookId;
            return ValueTask.CompletedTask;
        }
    }
}
