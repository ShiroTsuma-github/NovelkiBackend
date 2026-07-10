namespace Infrastructure.BookCovers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;

public sealed class BookCoverBackgroundService : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();
    private readonly InMemoryBookCoverQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookCoverBackgroundService> _logger;

    public BookCoverBackgroundService(
        InMemoryBookCoverQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BookCoverBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueTask = ProcessQueueAsync(stoppingToken);
        var pendingTask = ProcessPendingPeriodicallyAsync(stoppingToken);
        await Task.WhenAll(queueTask, pendingTask);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var bookId in _queue.ReadAllAsync(stoppingToken))
        {
            await ProcessBookAsync(bookId, stoppingToken);
        }
    }

    private async Task ProcessPendingPeriodicallyAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookCoverRepository>();
            var pending = await repository.GetPendingAsync(20, stoppingToken);
            foreach (var cover in pending)
            {
                await ProcessBookAsync(cover.BookId, stoppingToken);
            }
        }
    }

    private async Task ProcessBookAsync(Guid bookId, CancellationToken stoppingToken)
    {
        if (!_inFlight.TryAdd(bookId, 0))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<BookCoverProcessor>();
            await processor.ProcessAsync(bookId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug("Skipping stale cover job. BookId={BookId}", bookId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cover processing failed. BookId={BookId}", bookId);
        }
        finally
        {
            _inFlight.TryRemove(bookId, out _);
        }
    }
}

public sealed class BookCoverProcessor
{
    private readonly IBookCoverRepository _coverRepository;
    private readonly IBookCoverStorage _storage;
    private readonly BookCoverResolver _resolver;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BookCoverProcessor> _logger;

    public BookCoverProcessor(
        IBookCoverRepository coverRepository,
        IBookCoverStorage storage,
        BookCoverResolver resolver,
        IBookListCacheInvalidator cacheInvalidator,
        IHttpClientFactory httpClientFactory,
        ILogger<BookCoverProcessor> logger)
    {
        _coverRepository = coverRepository;
        _storage = storage;
        _resolver = resolver;
        _cacheInvalidator = cacheInvalidator;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var cover = await _coverRepository.GetByBookIdAsync(bookId, cancellationToken);
        if (cover == null || cover.Status != BookCoverStatus.Pending)
        {
            return;
        }

        cover.LastAttemptAt = DateTimeOffset.UtcNow;
        await _coverRepository.SaveAsync(cancellationToken);

        try
        {
            var candidate = await _resolver.FindAsync(cover.Book, cancellationToken);
            if (candidate == null)
            {
                cover.Status = BookCoverStatus.NotFound;
                cover.FailureReason = "No cover found in saved links, AniList, Jikan, Google Books, Open Library, or Wikidata.";
                CoverLinkHelper.TouchBook(cover.Book);
                await _coverRepository.SaveAsync(cancellationToken);
                return;
            }

            using var client = _httpClientFactory.CreateClient("BookCoverImages");
            await using var imageStream = await client.GetStreamAsync(candidate.ImageUrl, cancellationToken);
            var stored = await _storage.SaveAsync(
                cover.Book.OwnerId,
                cover.BookId,
                imageStream,
                Path.GetFileName(new Uri(candidate.ImageUrl).LocalPath),
                null,
                cancellationToken);

            cover.Status = BookCoverStatus.Found;
            cover.Source = candidate.Source;
            cover.StoragePath = stored.StoragePath;
            cover.OriginalImageUrl = candidate.ImageUrl;
            cover.MimeType = stored.MimeType;
            cover.SizeBytes = stored.SizeBytes;
            cover.Width = stored.Width;
            cover.Height = stored.Height;
            cover.FailureReason = null;
            CoverLinkHelper.EnsureCoverSourceLink(cover.Book, candidate.ImageUrl, candidate.Source);
            CoverLinkHelper.TouchBook(cover.Book);
            await _coverRepository.SaveAsync(cancellationToken);
            await _cacheInvalidator.InvalidateBooksAsync(cover.Book.OwnerId, cancellationToken);
            _logger.LogInformation("Cover found. BookId={BookId} Source={Source}", cover.BookId, candidate.Source);
        }
        catch (FluentValidation.ValidationException ex)
        {
            cover.Status = BookCoverStatus.Failed;
            cover.FailureReason = ex.Message;
            CoverLinkHelper.TouchBook(cover.Book);
            await _coverRepository.SaveAsync(cancellationToken);
            await _cacheInvalidator.InvalidateBooksAsync(cover.Book.OwnerId, cancellationToken);
        }
        catch (Exception ex)
        {
            var shouldInvalidateCache = true;
            if (IsProviderResponseFailure(ex))
            {
                cover.Status = BookCoverStatus.NotFound;
                cover.FailureReason = "No valid cover response was found from the configured providers.";
                shouldInvalidateCache = false;
            }
            else
            {
                cover.Status = BookCoverStatus.Failed;
                cover.FailureReason = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            }
            CoverLinkHelper.TouchBook(cover.Book);
            await _coverRepository.SaveAsync(cancellationToken);
            if (shouldInvalidateCache)
            {
                await _cacheInvalidator.InvalidateBooksAsync(cover.Book.OwnerId, cancellationToken);
            }
            _logger.LogWarning(ex, "Cover provider failed. BookId={BookId}", cover.BookId);
        }
    }

    private static bool IsProviderResponseFailure(Exception ex)
    {
        return ex is JsonException ||
               ex.Message.Contains("invalid start of a value", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("'<'", StringComparison.Ordinal);
    }
}

internal static class CoverLinkHelper
{
    public static void EnsureCoverSourceLink(Book book, string? imageUrl, BookCoverSource? source)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
        {
            return;
        }

        if (book.Links.Any(link => string.Equals(link.Url, imageUrl, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        book.Links.Add(new BookLink
        {
            Id = Guid.Empty,
            BookId = book.Id,
            Url = imageUrl,
            Label = source?.ToString(),
            SourceType = "Cover",
            IsPrimary = false,
            LastReadHere = false,
        });
    }

    public static void TouchBook(Book book)
    {
        book.LastModified = DateTimeOffset.UtcNow;
    }
}
