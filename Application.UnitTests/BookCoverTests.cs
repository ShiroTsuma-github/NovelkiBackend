using Application.Common.DTOs.Book;
using Application.Common.Interfaces;
using Application.Features.BookFeatures.Commands;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.BookCovers;
using System.Net;

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
        var book = CreateBook();
        var repository = new FakeBookRepository(book);
        var coverRepository = new FakeBookCoverRepository();
        var remoteImageService = new FakeRemoteImageService();
        var handler = new SetBookCoverFromUrlHandler(
            repository,
            coverRepository,
            new FakeCoverStorage(),
            remoteImageService,
            new FakeUser());

        var dto = await handler.Handle(new SetBookCoverFromUrlCommand(book.Id, "https://example.com/cover.jpg"), CancellationToken.None);

        Assert.Equal("Uploaded", dto.Status);
        Assert.Equal("ManualUrl", dto.Source);
        Assert.Equal("https://example.com/cover.jpg", book.Cover!.OriginalImageUrl);
        Assert.Equal("owner/book.jpg", book.Cover.StoragePath);
        Assert.True(coverRepository.Saved);
    }

    private static Book CreateBook()
    {
        return new Book
        {
            Id = BookId,
            OwnerId = OwnerId,
            PrimaryTitle = "I Shall Seal the Heavens",
            NormalizedPrimaryTitle = "I SHALL SEAL THE HEAVENS",
            ContentTypeId = Guid.NewGuid(),
            StatusId = Guid.NewGuid(),
            Cover = new BookCover { BookId = BookId }
        };
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
        private readonly string _html;

        public FakeHttpMessageHandler(string html)
        {
            _html = html;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, System.Text.Encoding.UTF8, "text/html")
            });
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

        public FakeBookRepository(Book book)
        {
            _book = book;
        }

        public Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(id == _book.Id && ownerId == _book.OwnerId ? _book : null);
        }

        public Task<Book?> GetByNameAsync(string name, Guid ownerId, CancellationToken cancellationToken) => Task.FromResult<Book?>(null);
        public Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Book>>(Array.Empty<Book>());
        public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(Book book, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeBookCoverRepository : IBookCoverRepository
    {
        public bool Saved { get; private set; }

        public Task<BookCover?> GetByBookIdAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken) => Task.FromResult<BookCover?>(null);
        public Task<BookCover?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken) => Task.FromResult<BookCover?>(null);
        public Task<IReadOnlyCollection<BookCover>> GetPendingAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<BookCover>>(Array.Empty<BookCover>());
        public Task AddAsync(BookCover cover, CancellationToken cancellationToken)
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

    private sealed class FakeCoverStorage : IBookCoverStorage
    {
        public Task<BookCoverStoredFile> SaveAsync(Guid ownerId, Guid bookId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BookCoverStoredFile("owner/book.jpg", "image/jpeg", 123, null, null));
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteIfExistsAsync(string? storagePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRemoteImageService : IBookCoverRemoteImageService
    {
        public Task<BookCoverStoredFile> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BookCoverStoredFile("owner/book.jpg", "image/jpeg", 123, null, null));
        }
    }
}
