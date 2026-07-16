using Application.Common.DTOs.Book;
using Application.Common.Models;
using Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class BookListCacheTests
{
    [Fact]
    public async Task GetBooksAsync_ShouldReturnNullWhenEntryDoesNotExist()
    {
        var cache = new BookListCache(new FakeDistributedCache(), NullLogger<BookListCache>.Instance);

        var result =
            await cache.GetBooksAsync(Guid.NewGuid(), 0, 20, null, null, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetBooksAsync_AndGetBooksAsync_ShouldUseNormalizedQueryAndSortInputs()
    {
        var ownerId = Guid.NewGuid();
        var storage = new FakeDistributedCache();
        var cache = new BookListCache(storage, NullLogger<BookListCache>.Instance);
        var expected = new PaginatedResult<BookListItemDto>
        {
            Skip = 0,
            Take = 20,
            Total = 1,
            Data =
            [
                new BookListItemDto
                {
                    Id = Guid.NewGuid(),
                    PrimaryTitle = "Lord of Mysteries",
                    ContentType = "Novel",
                    Status = "Reading",
                    AlternativeTitles = [],
                    Genres = [],
                    Tags = []
                }
            ]
        };

        await cache.SetBooksAsync(ownerId, 0, 20, "  title:lord  ", " LastModified ", " DESC ", expected,
            CancellationToken.None);
        var result = await cache.GetBooksAsync(ownerId, 0, 20, "title:lord",
            "lastmodified", "desc", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expected.Total, result.Total);
        Assert.Equal(expected.Data[0].PrimaryTitle, result.Data[0].PrimaryTitle);
        Assert.Contains(storage.StringKeys, key => key.Contains(":v1:"));
    }

    [Fact]
    public async Task InvalidateBooksAsync_ShouldBumpVersionAndHidePreviousEntries()
    {
        var ownerId = Guid.NewGuid();
        var storage = new FakeDistributedCache();
        var cache = new BookListCache(storage, NullLogger<BookListCache>.Instance);
        var initial = new PaginatedResult<BookListItemDto>
        {
            Skip = 0, Take = 20, Total = 1, Data = [CreateBookDto("Old Result")]
        };
        var refreshed = new PaginatedResult<BookListItemDto>
        {
            Skip = 0, Take = 20, Total = 1, Data = [CreateBookDto("New Result")]
        };

        await cache.SetBooksAsync(ownerId, 0, 20, "query", "title", "asc", initial, CancellationToken.None);
        await cache.InvalidateBooksAsync(ownerId, CancellationToken.None);

        var oldLookup =
            await cache.GetBooksAsync(ownerId, 0, 20, "query", "title", "asc", CancellationToken.None);
        await cache.SetBooksAsync(ownerId, 0, 20, "query", "title", "asc", refreshed, CancellationToken.None);
        var newLookup =
            await cache.GetBooksAsync(ownerId, 0, 20, "query", "title", "asc", CancellationToken.None);

        Assert.Null(oldLookup);
        Assert.NotNull(newLookup);
        Assert.Equal("New Result", newLookup.Data[0].PrimaryTitle);
        Assert.Contains(storage.StringEntries, pair => pair.Key == $"books:v2:{ownerId}:version" && pair.Value == "2");
    }

    [Fact]
    public async Task GetBooksAsync_ShouldReuseExistingPositiveVersion()
    {
        var ownerId = Guid.NewGuid();
        var storage = new FakeDistributedCache();
        storage.SetString($"books:v2:{ownerId}:version", "7");
        var cache = new BookListCache(storage, NullLogger<BookListCache>.Instance);

        await cache.GetBooksAsync(ownerId, 0, 10, null, null, null, CancellationToken.None);

        Assert.Equal("7", storage.StringEntries[$"books:v2:{ownerId}:version"]);
    }

    [Fact]
    public async Task GetBooksAsync_ShouldReinitializeInvalidVersionValue()
    {
        var ownerId = Guid.NewGuid();
        var storage = new FakeDistributedCache();
        storage.SetString($"books:v2:{ownerId}:version", "0");
        var cache = new BookListCache(storage, NullLogger<BookListCache>.Instance);

        await cache.GetBooksAsync(ownerId, 0, 10, null, null, null, CancellationToken.None);

        Assert.Equal("1", storage.StringEntries[$"books:v2:{ownerId}:version"]);
    }

    [Fact]
    public async Task SetAndGetBooksAsync_ShouldNormalizeNullAndWhitespaceQueryTheSameWay()
    {
        var ownerId = Guid.NewGuid();
        var storage = new FakeDistributedCache();
        var cache = new BookListCache(storage, NullLogger<BookListCache>.Instance);
        var expected = PaginatedResult<BookListItemDto>.Create(0, 10, 1, [CreateBookDto("Whitespace")]);

        await cache.SetBooksAsync(ownerId, 0, 10, "   ", null, null, expected, CancellationToken.None);
        var result =
            await cache.GetBooksAsync(ownerId, 0, 10, null, null, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Whitespace", result.Data[0].PrimaryTitle);
    }

    private static BookListItemDto CreateBookDto(string title)
    {
        return new BookListItemDto
        {
            Id = Guid.NewGuid(),
            PrimaryTitle = title,
            ContentType = "Novel",
            Status = "Reading",
            AlternativeTitles = [],
            Genres = [],
            Tags = []
        };
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> StringEntries =>
            _entries.ToDictionary(pair => pair.Key, pair => System.Text.Encoding.UTF8.GetString(pair.Value),
                StringComparer.Ordinal);

        public IReadOnlyCollection<string> StringKeys => _entries.Keys.ToArray();

        public byte[]? Get(string key)
        {
            return _entries.TryGetValue(key, out var value) ? value : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _entries.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _entries[key] = value;
        }

        public void SetString(string key, string value)
        {
            _entries[key] = System.Text.Encoding.UTF8.GetBytes(value);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }
    }
}
