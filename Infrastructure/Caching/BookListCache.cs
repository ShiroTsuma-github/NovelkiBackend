namespace Infrastructure.Caching;

using Application.Common.DTOs.Book;
using Application.Common.Models;
using Observability;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class BookListCache : IBookListCache, IBookListCacheInvalidator
{
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
        using Activity? activity =
            StartCacheActivity("cache.books.get", ownerId, skip, take, query, sortBy, sortDirection);
        string key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        activity?.SetTag("cache.key", key);

        PaginatedResult<BookListItemDto>? cached = await GetAsync<BookListItemDto>(key, cancellationToken);
        bool hit = cached != null;
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
        using Activity? activity =
            StartCacheActivity("cache.books.set", ownerId, skip, take, query, sortBy, sortDirection);
        string key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        activity?.SetTag("cache.key", key);
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
        using Activity? activity =
            InfrastructureTelemetry.ActivitySource.StartActivity("cache.books.invalidate", ActivityKind.Internal);
        activity?.SetTag("cache.type", "book-list");
        activity?.SetTag("cache.owner_id", ownerId);
        string versionKey = GetVersionKey(ownerId);
        int current = await GetVersionAsync(versionKey, cancellationToken);
        int next = current + 1;
        activity?.SetTag("cache.version.key", versionKey);
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
        string? json = await _cache.GetStringAsync(key, cancellationToken);
        return json == null ? null : JsonSerializer.Deserialize<PaginatedResult<T>>(json, SerializerOptions);
    }

    private Task SetAsync<T>(string key, PaginatedResult<T> value, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(value, SerializerOptions);
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
        int version = await GetVersionAsync(GetVersionKey(ownerId), cancellationToken);
        return
            $"books:{ownerId}:v{version}:skip:{skip}:take:{take}:sort:{Normalize(sortBy)}:{Normalize(sortDirection)}:q:{Hash(query)}";
    }

    private async Task<int> GetVersionAsync(string key, CancellationToken cancellationToken)
    {
        using Activity? activity =
            InfrastructureTelemetry.ActivitySource.StartActivity("cache.books.version.get", ActivityKind.Internal);
        activity?.SetTag("cache.version.key", key);
        string? value = await _cache.GetStringAsync(key, cancellationToken);
        if (int.TryParse(value, out int version) && version > 0)
        {
            activity?.SetTag("cache.version", version);
            activity?.SetTag("cache.version.initialized", false);
            return version;
        }

        await _cache.SetStringAsync(key, "1", CreateVersionOptions(), cancellationToken);
        activity?.SetTag("cache.version", 1);
        activity?.SetTag("cache.version.initialized", true);
        _logger.LogInformation("Book list cache version initialized. VersionKey={VersionKey} Version={Version}", key,
            1);
        return 1;
    }

    private static string GetVersionKey(Guid ownerId)
    {
        return $"books:{ownerId}:version";
    }

    private static string Hash(string? value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value?.Trim() ?? string.Empty));
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
        Activity? activity = InfrastructureTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("cache.type", "book-list");
        activity?.SetTag("cache.owner_id", ownerId);
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
