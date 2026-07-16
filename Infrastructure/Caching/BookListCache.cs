namespace Infrastructure.Caching;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.DTOs.Book;
using Application.Common.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Observability;

public sealed class BookListCache : IBookListCache, IBookListCacheInvalidator
{
    private const string CacheKeyTag = "cache.key";
    private const string CacheTypeTag = "cache.type";
    private const string CacheOwnerIdTag = "cache.owner_id";
    private const string CacheVersionKeyTag = "cache.version.key";
    private const string CacheVersionTag = "cache.version";
    private const string CacheVersionInitializedTag = "cache.version.initialized";
    private const string BookListCacheType = "book-list";
    private const string BookCacheKeyPrefix = "books";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;
    private readonly ILogger<BookListCache> _logger;

    public BookListCache(IDistributedCache cache, ILogger<BookListCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<PaginatedResult<BookListItemDto>?> GetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        using var activity =
            StartCacheActivity("cache.books.get", ownerId, skip, take, query, sortBy, sortDirection);
        var key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        activity?.SetTag(CacheKeyTag, key);

        var cached = await GetAsync<BookListItemDto>(key, cancellationToken);
        var hit = cached != null;
        activity?.SetTag("cache.hit", hit);
        _logger.LogInformation(
            "Book list cache lookup. OwnerId={OwnerId} Skip={Skip} Take={Take} SortBy={SortBy} SortDirection={SortDirection} Query={Query} CacheKey={CacheKey} CacheHit={CacheHit}",
            ownerId,
            skip,
            take,
            Normalize(sortBy),
            Normalize(sortDirection),
            query?.Trim() ?? string.Empty,
            key,
            hit);
        return cached;
    }

    public async Task SetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        PaginatedResult<BookListItemDto> value,
        CancellationToken cancellationToken)
    {
        using var activity =
            StartCacheActivity("cache.books.set", ownerId, skip, take, query, sortBy, sortDirection);
        var key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        activity?.SetTag(CacheKeyTag, key);
        activity?.SetTag("cache.item_count", value.Data.Count);
        await SetAsync(key, value, cancellationToken);
        _logger.LogInformation(
            "Book list cache set. OwnerId={OwnerId} Skip={Skip} Take={Take} SortBy={SortBy} SortDirection={SortDirection} Query={Query} CacheKey={CacheKey} ItemCount={ItemCount}",
            ownerId,
            skip,
            take,
            Normalize(sortBy),
            Normalize(sortDirection),
            query?.Trim() ?? string.Empty,
            key,
            value.Data.Count);
    }

    public async Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        using var activity =
            InfrastructureTelemetry.ActivitySource.StartActivity("cache.books.invalidate", ActivityKind.Internal);
        activity?.SetTag(CacheTypeTag, BookListCacheType);
        activity?.SetTag(CacheOwnerIdTag, ownerId);
        var versionKey = GetVersionKey(ownerId);
        var current = await GetVersionAsync(versionKey, cancellationToken);
        var next = current + 1;
        activity?.SetTag(CacheVersionKeyTag, versionKey);
        activity?.SetTag("cache.version.previous", current);
        activity?.SetTag("cache.version.next", next);
        await _cache.SetStringAsync(versionKey, next.ToString(), CreateVersionOptions(), cancellationToken);
        _logger.LogInformation(
            "Book list cache invalidated. OwnerId={OwnerId} VersionKey={VersionKey} PreviousVersion={PreviousVersion} NextVersion={NextVersion}",
            ownerId,
            versionKey,
            current,
            next);
    }

    private async Task<PaginatedResult<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);
        return json == null ? null : JsonSerializer.Deserialize<PaginatedResult<T>>(json, SerializerOptions);
    }

    private Task SetAsync<T>(string key, PaginatedResult<T> value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        return _cache.SetStringAsync(key, json, CreateEntryOptions(), cancellationToken);
    }

    private async Task<string> BuildBooksKey(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var version = await GetVersionAsync(GetVersionKey(ownerId), cancellationToken);
        return
            $"{BookCacheKeyPrefix}:{ownerId}:v{version}:skip:{skip}:take:{take}:sort:{Normalize(sortBy)}:{Normalize(sortDirection)}:q:{Hash(query)}";
    }

    private async Task<int> GetVersionAsync(string key, CancellationToken cancellationToken)
    {
        using var activity =
            InfrastructureTelemetry.ActivitySource.StartActivity("cache.books.version.get", ActivityKind.Internal);
        activity?.SetTag(CacheVersionKeyTag, key);
        var value = await _cache.GetStringAsync(key, cancellationToken);
        if (int.TryParse(value, out var version) && version > 0)
        {
            activity?.SetTag(CacheVersionTag, version);
            activity?.SetTag(CacheVersionInitializedTag, false);
            return version;
        }

        await _cache.SetStringAsync(key, "1", CreateVersionOptions(), cancellationToken);
        activity?.SetTag(CacheVersionTag, 1);
        activity?.SetTag(CacheVersionInitializedTag, true);
        _logger.LogInformation("Book list cache version initialized. VersionKey={VersionKey} Version={Version}", key,
            1);
        return 1;
    }

    private static string GetVersionKey(Guid ownerId)
    {
        return $"{BookCacheKeyPrefix}:{ownerId}:version";
    }

    private static string Hash(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value?.Trim() ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private static Activity? StartCacheActivity(
        string name,
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection)
    {
        var activity = InfrastructureTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag(CacheTypeTag, BookListCacheType);
        activity?.SetTag(CacheOwnerIdTag, ownerId);
        activity?.SetTag("cache.skip", skip);
        activity?.SetTag("cache.take", take);
        activity?.SetTag("cache.query", query?.Trim() ?? string.Empty);
        activity?.SetTag("cache.query.hash", Hash(query));
        activity?.SetTag("cache.sort_by", Normalize(sortBy));
        activity?.SetTag("cache.sort_direction", Normalize(sortDirection));
        return activity;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToLowerInvariant();
    }

    private static DistributedCacheEntryOptions CreateEntryOptions()
    {
        return new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
    }

    private static DistributedCacheEntryOptions CreateVersionOptions()
    {
        return new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };
    }
}
