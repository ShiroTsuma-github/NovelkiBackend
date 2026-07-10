namespace Infrastructure.Caching;

using Application.Common.DTOs.Book;
using Application.Common.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class BookListCache : IBookListCache, IBookListCacheInvalidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;

    public BookListCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<PaginatedResult<BookDto>?> GetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        return await GetAsync<BookDto>(key, cancellationToken);
    }

    public async Task SetBooksAsync(
        Guid ownerId,
        int skip,
        int take,
        string? query,
        string? sortBy,
        string? sortDirection,
        PaginatedResult<BookDto> value,
        CancellationToken cancellationToken)
    {
        var key = await BuildBooksKey(ownerId, skip, take, query, sortBy, sortDirection, cancellationToken);
        await SetAsync(key, value, cancellationToken);
    }

    public async Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var versionKey = GetVersionKey(ownerId);
        var current = await GetVersionAsync(versionKey, cancellationToken);
        await _cache.SetStringAsync(versionKey, (current + 1).ToString(), CreateVersionOptions(), cancellationToken);
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
        return $"books:{ownerId}:v{version}:skip:{skip}:take:{take}:sort:{Normalize(sortBy)}:{Normalize(sortDirection)}:q:{Hash(query)}";
    }

    private async Task<int> GetVersionAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _cache.GetStringAsync(key, cancellationToken);
        if (int.TryParse(value, out var version) && version > 0)
        {
            return version;
        }

        await _cache.SetStringAsync(key, "1", CreateVersionOptions(), cancellationToken);
        return 1;
    }

    private static string GetVersionKey(Guid ownerId) => $"books:{ownerId}:version";

    private static string Hash(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value?.Trim() ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToLowerInvariant();

    private static DistributedCacheEntryOptions CreateEntryOptions()
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
    }

    private static DistributedCacheEntryOptions CreateVersionOptions()
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
        };
    }
}
