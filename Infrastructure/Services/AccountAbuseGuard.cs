namespace Infrastructure.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class AccountAbuseGuard
{
    private const string CacheKeyPrefix = "security:account:block:";
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _localBlocks = new();
    private readonly ILogger<AccountAbuseGuard> _logger;
    private readonly BookImportSecurityOptions _options;

    public AccountAbuseGuard(
        IDistributedCache cache,
        IOptions<BookImportSecurityOptions> options,
        ILogger<AccountAbuseGuard> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ThrowIfBlockedAsync(IUser user, CancellationToken cancellationToken)
    {
        if (IsAdmin(user))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_localBlocks.TryGetValue(user.RequiredId, out var localBlockedUntil))
        {
            if (localBlockedUntil > now)
            {
                throw new AccountTemporarilyBlockedException(localBlockedUntil);
            }

            _localBlocks.TryRemove(user.RequiredId, out _);
        }

        try
        {
            var value = await _cache.GetStringAsync(CreateCacheKey(user.RequiredId), cancellationToken);
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
            {
                var blockedUntil = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                if (blockedUntil > now)
                {
                    _localBlocks[user.RequiredId] = blockedUntil;
                    throw new AccountTemporarilyBlockedException(blockedUntil);
                }

                await _cache.RemoveAsync(CreateCacheKey(user.RequiredId), cancellationToken);
            }
        }
        catch (AccountTemporarilyBlockedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Could not read the account abuse block for user {UserId}; local protection remains active.",
                user.RequiredId);
        }
    }

    public async Task<DateTimeOffset> BlockAsync(IUser user, string reasonCode,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(user))
        {
            return DateTimeOffset.UtcNow;
        }

        var blockedUntil = DateTimeOffset.UtcNow.Add(_options.SuspiciousAccountBlockDuration);
        _localBlocks[user.RequiredId] = blockedUntil;

        try
        {
            await _cache.SetStringAsync(
                CreateCacheKey(user.RequiredId),
                blockedUntil.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpiration = blockedUntil },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Could not persist the account abuse block for user {UserId}; local protection remains active.",
                user.RequiredId);
        }

        _logger.LogWarning(
            "Account abuse block applied to user {UserId}. ReasonCode={ReasonCode} BlockedUntilUtc={BlockedUntilUtc}",
            user.RequiredId, reasonCode, blockedUntil);
        return blockedUntil;
    }

    public static bool IsAdmin(IUser user)
    {
        return user.Roles.Contains(AuthorizationRoles.Admin, StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateCacheKey(Guid userId)
    {
        return $"{CacheKeyPrefix}{userId:N}";
    }
}
