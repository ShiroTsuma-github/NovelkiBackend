namespace Infrastructure.Services;

using System.Text.Json;
using System.Linq.Expressions;
using Application.Common.DTOs.Book;
using Application.Common.Models;
using Domain.Associations;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

public sealed class PublicBookService(
    ApplicationDbContext context,
    IUser user,
    IBookCoverStorage storage,
    IAuthorLifecycleService authorLifecycle,
    IBookListCacheInvalidator cacheInvalidator,
    IStorageCleanupQueue cleanupQueue) : IPublicBookService
{
    private const int MaxMutationAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PaginatedResult<PublicBookSnapshotDto>> SearchAsync(string? search, int skip, int take,
        bool mineOnly, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 50);
        skip = Math.Max(0, skip);
        var query = context.PublicBookSnapshots.AsNoTracking();
        if (mineOnly) query = query.Where(snapshot => snapshot.OwnerId == user.RequiredId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = ApplySearch(query, BookSearchQueryParser.Parse(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var snapshots = await query.OrderBy(snapshot => snapshot.PrimaryTitle).ThenBy(snapshot => snapshot.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);
        return PaginatedResult<PublicBookSnapshotDto>.Create(skip, take, total, snapshots.Select(ToDto));
    }

    public async Task<PublicBookSnapshotDto> PublishAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var existingId = await context.PublicBookSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SourceBookId == bookId)
            .Select(snapshot => (Guid?)snapshot.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
        {
            return await RefreshAsync(existingId.Value, cancellationToken);
        }

        var attempt = 0;
        while (true)
        {
            BookCoverStoredFiles? storedCover = null;
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            try
            {
                var concurrentSnapshot = await context.PublicBookSnapshots.FirstOrDefaultAsync(
                    snapshot => snapshot.SourceBookId == bookId, cancellationToken);
                if (concurrentSnapshot is not null)
                {
                    await CommitIfOwnedAsync(transaction, cancellationToken);
                    return ToDto(concurrentSnapshot);
                }

                var book = await LoadOwnedBookAsync(bookId, user.RequiredId, cancellationToken);
                EnsurePublishable(book);
                var snapshot = new PublicBookSnapshot
                {
                    Id = Guid.NewGuid(),
                    SourceBookId = book.Id,
                    OwnerId = book.OwnerId,
                    PrimaryTitle = book.PrimaryTitle,
                    NormalizedPrimaryTitle = book.NormalizedPrimaryTitle,
                    AlternativeTitlesJson = "[]",
                    AuthorOtherNamesJson = "[]",
                    ContentType = book.ContentType.Name,
                    GenresJson = "[]",
                    TagsJson = "[]",
                    PublicTagIdsJson = "[]",
                    SnapshotAt = DateTimeOffset.UtcNow
                };
                await ApplySnapshotAsync(snapshot, book, cancellationToken);
                storedCover = await StoreSnapshotCoverAsync(snapshot, book.Cover, cancellationToken);
                context.PublicBookSnapshots.Add(snapshot);
                await context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(transaction, cancellationToken);
                return ToDto(snapshot);
            }
            catch (DbUpdateException) when (attempt++ < MaxMutationAttempts - 1)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();

                var winner = await context.PublicBookSnapshots.AsNoTracking()
                    .FirstOrDefaultAsync(snapshot => snapshot.SourceBookId == bookId, cancellationToken);
                if (winner is not null)
                {
                    return ToDto(winner);
                }
            }
            catch
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();
                throw;
            }
        }
    }

    public async Task<PublicBookSnapshotDto> RefreshAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            BookCoverStoredFiles? storedCover = null;
            string? oldCoverPath = null;
            string? oldThumbnailPath = null;
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            try
            {
                var snapshot = await GetOwnedSnapshotAsync(snapshotId, cancellationToken);
                var oldAuthorId = snapshot.PublicAuthorId;
                var oldTagIds = Deserialize<Guid[]>(snapshot.PublicTagIdsJson);
                oldCoverPath = snapshot.CoverStoragePath;
                oldThumbnailPath = snapshot.CoverThumbnailStoragePath;
                var book = await LoadOwnedBookAsync(snapshot.SourceBookId, user.RequiredId, cancellationToken);
                EnsurePublishable(book);

                await ApplySnapshotAsync(snapshot, book, cancellationToken);
                storedCover = await StoreSnapshotCoverAsync(snapshot, book.Cover, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await CleanupPromotionsAsync(oldAuthorId, oldTagIds, snapshot.OwnerId, cancellationToken);
                await QueueReplacedFilesAsync(oldCoverPath, oldThumbnailPath, snapshot, cancellationToken);
                await CommitIfOwnedAsync(transaction, cancellationToken);
                return ToDto(snapshot);
            }
            catch (DbUpdateConcurrencyException) when (attempt++ < MaxMutationAttempts - 1)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();
                if (!await context.PublicBookSnapshots.AsNoTracking().AnyAsync(
                        snapshot => snapshot.Id == snapshotId && snapshot.OwnerId == user.RequiredId,
                        cancellationToken))
                {
                    throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
                }
            }
            catch (DbUpdateException) when (attempt++ < MaxMutationAttempts - 1)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();
            }
            catch
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();
                throw;
            }
        }
    }

    public async Task UnlistAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var snapshot = await GetOwnedSnapshotAsync(snapshotId, cancellationToken);
            await RemoveSnapshotsAsync([snapshot], cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackIfOwnedAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
        }
        catch
        {
            await RollbackIfOwnedAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task UnlistBySourceBookAsync(Guid bookId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var snapshot = await context.PublicBookSnapshots.FirstOrDefaultAsync(
                item => item.SourceBookId == bookId && item.OwnerId == user.RequiredId, cancellationToken);
            if (snapshot is not null)
            {
                await RemoveSnapshotsAsync([snapshot], cancellationToken);
            }

            await CommitIfOwnedAsync(transaction, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackIfOwnedAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            return;
        }
        catch
        {
            await RollbackIfOwnedAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task UnlistAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var snapshots = await context.PublicBookSnapshots.Where(snapshot => snapshot.OwnerId == ownerId)
                .ToListAsync(cancellationToken);
            await RemoveSnapshotsAsync(snapshots, cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
        }
        catch
        {
            await RollbackIfOwnedAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<CopyPublicBookResult> CopyAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            BookCoverStoredFiles? storedCover = null;
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            try
            {
                var snapshot = await context.PublicBookSnapshots.AsNoTracking()
                                   .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken)
                               ?? throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
                var type = await context.ContentTypes.FirstOrDefaultAsync(item => item.Name == snapshot.ContentType,
                               cancellationToken)
                           ?? throw new ValidationException(
                               $"Content type '{snapshot.ContentType}' is no longer available.");
                if (await HasDuplicateBookAsync(snapshot, type.Id, cancellationToken))
                {
                    throw DuplicateBookException();
                }

                var status = await context.Statuses.FirstAsync(item => item.Slug == "plan-to-read", cancellationToken);
                var book = new Book
                {
                    Id = Guid.NewGuid(),
                    OwnerId = user.RequiredId,
                    PrimaryTitle = snapshot.PrimaryTitle,
                    NormalizedPrimaryTitle = snapshot.NormalizedPrimaryTitle,
                    Description = snapshot.Description,
                    ContentTypeId = type.Id,
                    ContentType = type,
                    StatusId = status.Id,
                    Status = status,
                    CurrentChapterNumber = 0,
                    TotalChapters = snapshot.TotalChapters
                };
                book.Titles.Add(snapshot.PrimaryTitle.ToPrimaryTitle());
                foreach (var title in Deserialize<string[]>(snapshot.AlternativeTitlesJson)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    book.Titles.Add(new BookTitle
                    {
                        Title = title,
                        NormalizedTitle = MappingExtensions.NormalizeName(title),
                        IsPrimary = false,
                        Source = "Public snapshot"
                    });
                }

                await AttachAuthorAsync(book, snapshot, cancellationToken);
                await AttachGenresAsync(book, Deserialize<PublicBookMetadataDto[]>(snapshot.GenresJson),
                    cancellationToken);
                await AttachTagsAsync(book, Deserialize<PublicBookMetadataDto[]>(snapshot.TagsJson),
                    cancellationToken);
                context.Books.Add(book);

                if (!string.IsNullOrWhiteSpace(snapshot.CoverStoragePath))
                {
                    await using var coverStream = await storage.OpenReadAsync(snapshot.CoverStoragePath,
                        cancellationToken);
                    storedCover = await storage.SaveAsync(book.OwnerId, book.Id, coverStream, "cover.jpg",
                        snapshot.CoverMimeType, cancellationToken);
                    context.BookCovers.Add(CreateBookCover(book, storedCover));
                }

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(transaction, cancellationToken);
                await cacheInvalidator.InvalidateBooksAsync(user.RequiredId, cancellationToken);
                return new CopyPublicBookResult(book.Id);
            }
            catch (DbUpdateException) when (attempt++ < MaxMutationAttempts - 1)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();

                var snapshot = await context.PublicBookSnapshots.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);
                if (snapshot is null)
                {
                    throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
                }

                var typeId = await context.ContentTypes.AsNoTracking()
                    .Where(item => item.Name == snapshot.ContentType)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (typeId.HasValue && await HasDuplicateBookAsync(snapshot, typeId.Value, cancellationToken))
                {
                    throw DuplicateBookException();
                }
            }
            catch
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                await QueueCompensatingCleanupAsync(storedCover, cancellationToken);
                context.ChangeTracker.Clear();
                throw;
            }
        }
    }

    public async Task<(Stream Content, string MimeType)> OpenCoverAsync(Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var snapshot = await context.PublicBookSnapshots.AsNoTracking().FirstOrDefaultAsync(item => item.Id == snapshotId,
                           cancellationToken)
                       ?? throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
        if (string.IsNullOrWhiteSpace(snapshot.CoverStoragePath))
        {
            throw new EntityNotFoundException<PublicBookSnapshot, Guid>(snapshotId);
        }

        return (await storage.OpenReadAsync(snapshot.CoverStoragePath, cancellationToken),
            snapshot.CoverMimeType ?? "image/jpeg");
    }

    private async Task ApplySnapshotAsync(PublicBookSnapshot snapshot, Book book,
        CancellationToken cancellationToken)
    {
        var previousSnapshotAt = snapshot.SnapshotAt;
        snapshot.PrimaryTitle = book.PrimaryTitle;
        snapshot.NormalizedPrimaryTitle = book.NormalizedPrimaryTitle;
        snapshot.Description = book.Description;
        snapshot.AlternativeTitlesJson = Serialize(book.Titles.Where(title => !title.IsPrimary)
            .Select(title => title.Title));
        snapshot.AuthorName = book.Author?.PrimaryName;
        snapshot.AuthorOtherNamesJson = Serialize(book.Author?.Names.Where(name => !name.IsPrimary)
            .Select(name => name.Name) ?? []);
        snapshot.PublicAuthorId = await PublishAuthorAsync(book.Author, cancellationToken);
        snapshot.ContentType = book.ContentType.Name;
        snapshot.TotalChapters = book.TotalChapters;
        snapshot.GenresJson = Serialize(book.BookGenres.Select(link =>
            new PublicBookMetadataDto(link.Genre.Name, link.Genre.Description)));
        var tags = book.BookTags.Select(link => link.Tag).ToList();
        snapshot.TagsJson = Serialize(tags.Select(tag => new PublicBookMetadataDto(tag.Name, tag.Description)));
        snapshot.PublicTagIdsJson = Serialize(await PublishTagsAsync(tags, cancellationToken));
        snapshot.SnapshotAt = NextSnapshotAt(previousSnapshotAt);
    }

    private async Task<Guid?> PublishAuthorAsync(Author? author, CancellationToken cancellationToken)
    {
        if (author is null) return null;
        if (author.IsPublic) return author.Id;
        var names = author.Names.Select(name => name.NormalizedName).Append(author.NormalizedPrimaryName).Distinct()
            .ToArray();
        var existing = await context.Authors.Include(item => item.Names).FirstOrDefaultAsync(item => item.IsPublic &&
            (names.Contains(item.NormalizedPrimaryName) || item.Names.Any(name => names.Contains(name.NormalizedName))),
            cancellationToken);
        if (existing is not null) return existing.Id;
        author.IsPublic = true;
        context.BookShareAuthorPromotions.Add(new BookShareAuthorPromotion { AuthorId = author.Id, Author = author });
        return author.Id;
    }

    private async Task<Guid[]> PublishTagsAsync(IEnumerable<Tag> tags, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        foreach (var tag in tags)
        {
            if (tag.IsGlobal)
            {
                ids.Add(tag.Id);
                continue;
            }

            var existing = await context.Tags.FirstOrDefaultAsync(
                item => item.IsGlobal && item.NormalizedName == tag.NormalizedName, cancellationToken);
            if (existing is not null)
            {
                ids.Add(existing.Id);
                continue;
            }

            tag.IsGlobal = true;
            context.BookShareTagPromotions.Add(new BookShareTagPromotion { TagId = tag.Id, Tag = tag });
            ids.Add(tag.Id);
        }

        return ids.ToArray();
    }

    private async Task RemoveSnapshotsAsync(IReadOnlyCollection<PublicBookSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var promotions = snapshots.Select(snapshot => new
        {
            snapshot.PublicAuthorId,
            TagIds = Deserialize<Guid[]>(snapshot.PublicTagIdsJson),
            snapshot.OwnerId
        }).ToList();
        var storagePaths = snapshots.SelectMany(snapshot =>
                new[] { snapshot.CoverStoragePath, snapshot.CoverThumbnailStoragePath })
            .Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().Distinct().ToArray();

        context.PublicBookSnapshots.RemoveRange(snapshots);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var promotion in promotions)
        {
            await CleanupPromotionsAsync(promotion.PublicAuthorId, promotion.TagIds, promotion.OwnerId,
                cancellationToken);
        }
        await cleanupQueue.EnqueueAsync(storagePaths, cancellationToken);
    }

    private async Task CleanupPromotionsAsync(Guid? authorId, IEnumerable<Guid> tagIds, Guid promotionOwnerId,
        CancellationToken cancellationToken)
    {
        var snapshots = await context.PublicBookSnapshots.AsNoTracking()
            .Select(item => new { item.PublicAuthorId, item.PublicTagIdsJson }).ToListAsync(cancellationToken);
        if (authorId.HasValue && snapshots.All(item => item.PublicAuthorId != authorId))
        {
            var marker = await context.BookShareAuthorPromotions.FindAsync([authorId.Value], cancellationToken);
            if (marker is not null)
            {
                context.BookShareAuthorPromotions.Remove(marker);
                await context.SaveChangesAsync(cancellationToken);
                await authorLifecycle.SetVisibilityAsync(authorId.Value, promotionOwnerId, false, false,
                    cancellationToken);
                var unusedAuthor = await context.Authors.FirstOrDefaultAsync(item => item.Id == authorId.Value,
                    cancellationToken);
                if (unusedAuthor is not null && !await context.Books.AnyAsync(
                        book => book.AuthorId == authorId.Value, cancellationToken))
                {
                    context.Authors.Remove(unusedAuthor);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
        }

        var usedTagIds = snapshots.SelectMany(item => Deserialize<Guid[]>(item.PublicTagIdsJson)).ToHashSet();
        foreach (var tagId in tagIds.Distinct().Where(id => !usedTagIds.Contains(id)))
        {
            await LocalizeAutoTagAsync(tagId, cancellationToken);
        }
    }

    private async Task LocalizeAutoTagAsync(Guid tagId, CancellationToken cancellationToken)
    {
        var marker = await context.BookShareTagPromotions.FindAsync([tagId], cancellationToken);
        if (marker is null) return;
        var tag = await context.Tags.Include(item => item.BookTags).ThenInclude(link => link.Book)
            .FirstAsync(item => item.Id == tagId, cancellationToken);
        var ownerToKeep = tag.OwnerId;
        foreach (var group in tag.BookTags.Where(link => link.Book.OwnerId != ownerToKeep)
                     .GroupBy(link => link.Book.OwnerId).ToList())
        {
            var local = await context.Tags.FirstOrDefaultAsync(item => !item.IsGlobal && item.OwnerId == group.Key &&
                item.NormalizedName == tag.NormalizedName, cancellationToken);
            if (local is null)
            {
                local = new Tag
                {
                    OwnerId = group.Key,
                    IsGlobal = false,
                    Name = tag.Name,
                    NormalizedName = tag.NormalizedName,
                    Description = tag.Description,
                    Color = tag.Color
                };
                context.Tags.Add(local);
            }

            await context.SaveChangesAsync(cancellationToken);
            foreach (var link in group)
            {
                context.Remove(link);
                context.Add(new BookTag { BookId = link.BookId, TagId = local.Id });
            }

            await cacheInvalidator.InvalidateBooksAsync(group.Key, cancellationToken);
        }

        context.BookShareTagPromotions.Remove(marker);
        if (tag.BookTags.Any(link => link.Book.OwnerId == ownerToKeep)) tag.IsGlobal = false;
        else context.Tags.Remove(tag);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<BookCoverStoredFiles?> StoreSnapshotCoverAsync(PublicBookSnapshot snapshot, BookCover? cover,
        CancellationToken cancellationToken)
    {
        if (cover?.StoragePath is null)
        {
            snapshot.CoverStoragePath = null;
            snapshot.CoverThumbnailStoragePath = null;
            snapshot.CoverMimeType = null;
            return null;
        }

        await using var source = await storage.OpenReadAsync(cover.StoragePath, cancellationToken);
        var stored = await storage.SaveAsync(snapshot.OwnerId, Guid.NewGuid(), source, "snapshot-cover.jpg",
            cover.MimeType, cancellationToken);
        snapshot.CoverStoragePath = stored.Original.StoragePath;
        snapshot.CoverThumbnailStoragePath = stored.Thumbnail.StoragePath;
        snapshot.CoverMimeType = stored.Original.MimeType;
        return stored;
    }

    private async Task AttachAuthorAsync(Book book, PublicBookSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshot.AuthorName)) return;
        var normalizedNames = Deserialize<string[]>(snapshot.AuthorOtherNamesJson)
            .Append(snapshot.AuthorName)
            .Select(MappingExtensions.NormalizeName)
            .Distinct()
            .ToArray();
        var author = await context.Authors.Include(item => item.Names)
            .Where(item => item.IsPublic || item.OwnerId == user.RequiredId)
            .OrderBy(item => item.IsPublic)
            .FirstOrDefaultAsync(item => normalizedNames.Contains(item.NormalizedPrimaryName) ||
                                         item.Names.Any(name => normalizedNames.Contains(name.NormalizedName)),
                cancellationToken);
        if (author is null)
        {
            var normalized = MappingExtensions.NormalizeName(snapshot.AuthorName);
            author = new Author
            {
                OwnerId = user.RequiredId,
                IsPublic = false,
                PrimaryName = snapshot.AuthorName,
                NormalizedPrimaryName = normalized
            };
            author.Names.Add(new AuthorName
            {
                Name = snapshot.AuthorName,
                NormalizedName = normalized,
                IsPrimary = true,
                Source = "Public snapshot"
            });
            foreach (var alias in Deserialize<string[]>(snapshot.AuthorOtherNamesJson)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizedAlias = MappingExtensions.NormalizeName(alias);
                if (normalizedAlias == normalized)
                {
                    continue;
                }

                author.Names.Add(new AuthorName
                {
                    Name = alias,
                    NormalizedName = normalizedAlias,
                    IsPrimary = false,
                    Source = "Public snapshot"
                });
            }

            context.Authors.Add(author);
        }

        book.Author = author;
    }

    private async Task AttachGenresAsync(Book book, IEnumerable<PublicBookMetadataDto> genres,
        CancellationToken cancellationToken)
    {
        var names = genres.Select(item => MappingExtensions.NormalizeName(item.Name)).ToArray();
        var entities = await context.Genres.Where(item => names.Contains(item.NormalizedName))
            .ToListAsync(cancellationToken);
        foreach (var genre in entities)
        {
            book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        }
    }

    private async Task AttachTagsAsync(Book book, IEnumerable<PublicBookMetadataDto> tags,
        CancellationToken cancellationToken)
    {
        foreach (var item in tags)
        {
            var normalized = MappingExtensions.NormalizeName(item.Name);
            var tag = await context.Tags.Where(entity => entity.IsGlobal || entity.OwnerId == user.RequiredId)
                .OrderBy(entity => entity.IsGlobal)
                .FirstOrDefaultAsync(entity => entity.NormalizedName == normalized, cancellationToken);
            if (tag is null)
            {
                tag = new Tag
                {
                    OwnerId = user.RequiredId,
                    Name = item.Name,
                    NormalizedName = normalized,
                    Description = item.Description,
                    IsGlobal = false
                };
                context.Tags.Add(tag);
            }

            book.BookTags.Add(new BookTag { Book = book, Tag = tag });
        }
    }

    private async Task<Book> LoadOwnedBookAsync(Guid bookId, Guid ownerId, CancellationToken cancellationToken) =>
        await context.Books.Include(book => book.Author).ThenInclude(author => author!.Names)
            .Include(book => book.ContentType)
            .Include(book => book.Titles)
            .Include(book => book.BookGenres).ThenInclude(link => link.Genre)
            .Include(book => book.BookTags).ThenInclude(link => link.Tag)
            .Include(book => book.Cover)
            .FirstOrDefaultAsync(book => book.Id == bookId && book.OwnerId == ownerId, cancellationToken)
        ?? throw new EntityNotFoundException<Book, Guid>(bookId);

    private static void EnsurePublishable(Book book)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(book.Description)) missing.Add("description");
        if (book.Author is null) missing.Add("author");
        if (book.BookGenres.Count == 0) missing.Add("genre");
        if (book.BookTags.Count == 0) missing.Add("tag");
        if (book.Cover is null ||
            book.Cover.Status is not (BookCoverStatus.Found or BookCoverStatus.Uploaded) ||
            string.IsNullOrWhiteSpace(book.Cover.StoragePath))
        {
            missing.Add("stored cover");
        }

        if (missing.Count > 0)
        {
            throw new ValidationException(
                $"The book cannot be listed publicly. Add: {string.Join(", ", missing)}.");
        }
    }

    private static IQueryable<PublicBookSnapshot> ApplySearch(
        IQueryable<PublicBookSnapshot> query,
        BookSearchCriteria criteria)
    {
        if (criteria.Missing.Count > 0 || criteria.Dates.Count > 0 ||
            criteria.Fields.Any(filter => filter.Field == BookSearchField.Status) ||
            criteria.Numbers.Any(filter => filter.Field != BookSearchNumberField.TotalChapters) ||
            criteria.Terms.Any(term => term.Contains(':', StringComparison.Ordinal)))
        {
            throw new ValidationException(
                "Discover search supports title, author, description, genre, tag, type, and totalChapters. " +
                "Missing metadata, rating, current chapter, label, status, priority, and date filters are private and unavailable.");
        }

        var predicates = criteria.Terms.Select(BuildGeneralSearchPredicate)
            .Concat(criteria.Fields.Select(BuildFieldSearchPredicate))
            .Concat(criteria.Numbers.Select(BuildNumberSearchPredicate));
        var predicate = PredicateExpression.AndAll(predicates);
        return predicate is null ? query : query.Where(predicate);
    }

    private static Expression<Func<PublicBookSnapshot, bool>> BuildGeneralSearchPredicate(string value)
    {
        var normalized = MappingExtensions.NormalizeName(value);
        return snapshot => snapshot.NormalizedPrimaryTitle.Contains(normalized) ||
                           snapshot.AlternativeTitlesJson.ToUpper().Contains(normalized) ||
                           (snapshot.AuthorName != null && snapshot.AuthorName.ToUpper().Contains(normalized)) ||
                           snapshot.AuthorOtherNamesJson.ToUpper().Contains(normalized) ||
                           (snapshot.Description != null && snapshot.Description.ToUpper().Contains(normalized)) ||
                           snapshot.ContentType.ToUpper().Contains(normalized) ||
                           snapshot.GenresJson.ToUpper().Contains(normalized) ||
                           snapshot.TagsJson.ToUpper().Contains(normalized);
    }

    private static Expression<Func<PublicBookSnapshot, bool>> BuildFieldSearchPredicate(BookSearchFieldFilter filter)
    {
        return PredicateExpression.OrAll(filter.Values.Select(value =>
        {
            var normalized = MappingExtensions.NormalizeName(value);
            return filter.Field switch
            {
                BookSearchField.Title =>
                    (Expression<Func<PublicBookSnapshot, bool>>)(snapshot =>
                        snapshot.NormalizedPrimaryTitle.Contains(normalized) ||
                        snapshot.AlternativeTitlesJson.ToUpper().Contains(normalized)),
                BookSearchField.Author => snapshot =>
                    (snapshot.AuthorName != null && snapshot.AuthorName.ToUpper().Contains(normalized)) ||
                    snapshot.AuthorOtherNamesJson.ToUpper().Contains(normalized),
                BookSearchField.Description => snapshot =>
                    snapshot.Description != null && snapshot.Description.ToUpper().Contains(normalized),
                BookSearchField.Genre => snapshot => snapshot.GenresJson.ToUpper().Contains(normalized),
                BookSearchField.Tag => snapshot => snapshot.TagsJson.ToUpper().Contains(normalized),
                BookSearchField.Type => snapshot => snapshot.ContentType.ToUpper().Contains(normalized),
                _ => snapshot => false
            };
        })) ?? (_ => false);
    }

    private static Expression<Func<PublicBookSnapshot, bool>> BuildNumberSearchPredicate(BookSearchNumberFilter filter)
    {
        return filter.Operator switch
        {
            BookSearchOperator.GreaterThan => snapshot => snapshot.TotalChapters > filter.Value,
            BookSearchOperator.GreaterThanOrEqual => snapshot => snapshot.TotalChapters >= filter.Value,
            BookSearchOperator.LessThan => snapshot => snapshot.TotalChapters < filter.Value,
            BookSearchOperator.LessThanOrEqual => snapshot => snapshot.TotalChapters <= filter.Value,
            _ => snapshot => snapshot.TotalChapters == filter.Value
        };
    }

    private async Task<PublicBookSnapshot> GetOwnedSnapshotAsync(Guid id, CancellationToken cancellationToken) =>
        await context.PublicBookSnapshots.FirstOrDefaultAsync(item => item.Id == id && item.OwnerId == user.RequiredId,
            cancellationToken) ?? throw new EntityNotFoundException<PublicBookSnapshot, Guid>(id);

    private Task<bool> HasDuplicateBookAsync(PublicBookSnapshot snapshot, Guid contentTypeId,
        CancellationToken cancellationToken) => context.Books.AnyAsync(book => book.OwnerId == user.RequiredId &&
        book.NormalizedPrimaryTitle == snapshot.NormalizedPrimaryTitle && book.ContentTypeId == contentTypeId,
        cancellationToken);

    private PublicBookSnapshotDto ToDto(PublicBookSnapshot snapshot) => new()
    {
        Id = snapshot.Id,
        SourceBookId = snapshot.SourceBookId,
        PrimaryTitle = snapshot.PrimaryTitle,
        Description = snapshot.Description,
        AlternativeTitles = Deserialize<string[]>(snapshot.AlternativeTitlesJson),
        Author = snapshot.AuthorName,
        AuthorOtherNames = Deserialize<string[]>(snapshot.AuthorOtherNamesJson),
        ContentType = snapshot.ContentType,
        TotalChapters = snapshot.TotalChapters,
        Genres = Deserialize<PublicBookMetadataDto[]>(snapshot.GenresJson),
        Tags = Deserialize<PublicBookMetadataDto[]>(snapshot.TagsJson),
        SnapshotAt = snapshot.SnapshotAt,
        CoverUrl = snapshot.CoverStoragePath is null
            ? null
            : ApiRoutes.PublicBookCover(snapshot.Id, snapshot.SnapshotAt.ToUnixTimeMilliseconds()),
        IsOwner = snapshot.OwnerId == user.RequiredId
    };

    private static BookCover CreateBookCover(Book book, BookCoverStoredFiles stored) => new()
    {
        BookId = book.Id,
        Book = book,
        Status = BookCoverStatus.Uploaded,
        Source = BookCoverSource.ManualUpload,
        StoragePath = stored.Original.StoragePath,
        ThumbnailStoragePath = stored.Thumbnail.StoragePath,
        MimeType = stored.Original.MimeType,
        ThumbnailMimeType = stored.Thumbnail.MimeType,
        SizeBytes = stored.Original.SizeBytes,
        ThumbnailSizeBytes = stored.Thumbnail.SizeBytes,
        Width = stored.Original.Width,
        Height = stored.Original.Height,
        ThumbnailWidth = stored.Thumbnail.Width,
        ThumbnailHeight = stored.Thumbnail.Height
    };

    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static async Task CommitIfOwnedAsync(IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
    }

    private static async Task RollbackIfOwnedAsync(IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
    }

    private Task QueueReplacedFilesAsync(string? oldCoverPath, string? oldThumbnailPath,
        PublicBookSnapshot snapshot, CancellationToken cancellationToken)
    {
        var paths = new[] { oldCoverPath, oldThumbnailPath }
            .Where(path => !string.IsNullOrWhiteSpace(path) &&
                           path != snapshot.CoverStoragePath && path != snapshot.CoverThumbnailStoragePath)
            .Cast<string>()
            .Distinct()
            .ToArray();
        return cleanupQueue.EnqueueAsync(paths, cancellationToken);
    }

    private async Task QueueCompensatingCleanupAsync(BookCoverStoredFiles? stored,
        CancellationToken cancellationToken)
    {
        if (stored is null)
        {
            return;
        }

        try
        {
            await cleanupQueue.EnqueueAsync(
                [stored.Original.StoragePath, stored.Thumbnail.StoragePath], cancellationToken);
        }
        catch
        {
            try
            {
                await storage.DeleteIfExistsAsync(stored.Original.StoragePath, cancellationToken);
                await storage.DeleteIfExistsAsync(stored.Thumbnail.StoragePath, cancellationToken);
            }
            catch
            {
                // Both durable and immediate cleanup are best-effort when the database and storage are unavailable.
            }
        }
    }

    private static DateTimeOffset NextSnapshotAt(DateTimeOffset previous)
    {
        var now = DateTimeOffset.UtcNow;
        return now > previous ? now : previous.AddTicks(10);
    }

    private static ValidationException DuplicateBookException() =>
        new("This book already exists in your library.");

    private static string Serialize<T>(IEnumerable<T> values) => JsonSerializer.Serialize(values, JsonOptions);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)!;
}
