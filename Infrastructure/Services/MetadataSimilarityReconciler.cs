namespace Infrastructure.Services;

using System.Text.Json;
using Application.Common.DTOs.Book;

public sealed class MetadataSimilarityReconciler(
    ApplicationDbContext context,
    IBookListCacheInvalidator cacheInvalidator)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var affectedOwners = new HashSet<Guid>();
        var genreNames = await MergeGenresAsync(affectedOwners, cancellationToken);
        var (tagIds, tagNames) = await MergeTagsAsync(affectedOwners, cancellationToken);
        await RewriteSnapshotsAsync(genreNames, tagIds, tagNames, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var ownerId in affectedOwners)
        {
            await cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
        }
    }

    private async Task<Dictionary<string, Genre>> MergeGenresAsync(
        ISet<Guid> affectedOwners,
        CancellationToken cancellationToken)
    {
        var genres = await context.Genres.Include(genre => genre.BookGenres).ThenInclude(link => link.Book)
            .ToListAsync(cancellationToken);
        var replacements = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
        var ordered = genres.OrderByDescending(genre => genre.BookGenres.Count)
            .ThenByDescending(genre => !string.IsNullOrWhiteSpace(genre.Description))
            .ThenByDescending(genre => DisplayNameQuality(genre.Name))
            .ThenBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in BuildGroups(ordered, genre => genre.Name))
        {
            var winner = group[0];
            foreach (var duplicate in group.Skip(1))
            {
                MergeGenre(winner, duplicate, affectedOwners);
                replacements[duplicate.Name] = winner;
            }
        }

        return replacements;
    }

    private void MergeGenre(Genre winner, Genre duplicate, ISet<Guid> affectedOwners)
    {
        winner.Description ??= duplicate.Description;
        var linkedBooks = winner.BookGenres.Select(link => link.BookId).ToHashSet();
        foreach (var link in duplicate.BookGenres.ToList())
        {
            affectedOwners.Add(link.Book.OwnerId);
            context.Remove(link);
            if (linkedBooks.Add(link.BookId))
            {
                winner.BookGenres.Add(new BookGenre { BookId = link.BookId, GenreId = winner.Id });
            }
        }

        context.Genres.Remove(duplicate);
    }

    private async Task<(Dictionary<Guid, Guid> Ids, Dictionary<string, Tag> Names)> MergeTagsAsync(
        ISet<Guid> affectedOwners,
        CancellationToken cancellationToken)
    {
        var tags = await context.Tags.Include(tag => tag.BookTags).ThenInclude(link => link.Book)
            .ToListAsync(cancellationToken);
        var promotionIds = await context.BookShareTagPromotions.Select(marker => marker.TagId)
            .ToHashSetAsync(cancellationToken);
        var idReplacements = new Dictionary<Guid, Guid>();
        var nameReplacements = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        var globalWinners = MergeTagScope(
            tags.Where(tag => tag.IsGlobal)
                .OrderBy(tag => promotionIds.Contains(tag.Id))
                .ThenByDescending(tag => tag.BookTags.Count)
                .ThenByDescending(tag => DisplayNameQuality(tag.Name))
                .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            promotionIds,
            affectedOwners,
            idReplacements,
            nameReplacements);

        var privateWinners = new List<Tag>();
        foreach (var ownerTags in tags.Where(tag => !tag.IsGlobal && tag.OwnerId.HasValue)
                     .GroupBy(tag => tag.OwnerId!.Value))
        {
            privateWinners.AddRange(MergeTagScope(
                ownerTags.OrderByDescending(tag => tag.BookTags.Count)
                    .ThenByDescending(tag => DisplayNameQuality(tag.Name))
                    .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                promotionIds,
                affectedOwners,
                idReplacements,
                nameReplacements));
        }

        foreach (var privateTag in privateWinners)
        {
            var global = globalWinners
                .Where(tag => MetadataNameSimilarity.IsPracticalMatch(tag.Name, privateTag.Name))
                .OrderBy(tag => MetadataNameSimilarity.MatchDistance(tag.Name, privateTag.Name))
                .ThenByDescending(tag => tag.BookTags.Count)
                .FirstOrDefault();
            if (global != null)
            {
                MergeTag(global, privateTag, promotionIds, affectedOwners, idReplacements, nameReplacements);
            }
        }

        return (idReplacements, nameReplacements);
    }

    private List<Tag> MergeTagScope(
        IReadOnlyCollection<Tag> ordered,
        ISet<Guid> promotionIds,
        ISet<Guid> affectedOwners,
        IDictionary<Guid, Guid> idReplacements,
        IDictionary<string, Tag> nameReplacements)
    {
        var winners = new List<Tag>();
        foreach (var tag in ordered)
        {
            var winner = winners
                .Where(candidate => MetadataNameSimilarity.IsPracticalMatch(candidate.Name, tag.Name))
                .OrderBy(candidate => MetadataNameSimilarity.MatchDistance(candidate.Name, tag.Name))
                .FirstOrDefault();
            if (winner == null)
            {
                winners.Add(tag);
            }
            else
            {
                MergeTag(winner, tag, promotionIds, affectedOwners, idReplacements, nameReplacements);
            }
        }

        return winners;
    }

    private void MergeTag(
        Tag winner,
        Tag duplicate,
        ISet<Guid> promotionIds,
        ISet<Guid> affectedOwners,
        IDictionary<Guid, Guid> idReplacements,
        IDictionary<string, Tag> nameReplacements)
    {
        winner.Description ??= duplicate.Description;
        winner.Color ??= duplicate.Color;
        var linkedBooks = winner.BookTags.Select(link => link.BookId).ToHashSet();
        foreach (var link in duplicate.BookTags.ToList())
        {
            affectedOwners.Add(link.Book.OwnerId);
            context.Remove(link);
            if (linkedBooks.Add(link.BookId))
            {
                winner.BookTags.Add(new BookTag { BookId = link.BookId, TagId = winner.Id });
            }
        }

        if (promotionIds.Remove(duplicate.Id))
        {
            var marker = context.BookShareTagPromotions.Local.FirstOrDefault(item => item.TagId == duplicate.Id);
            if (marker != null)
            {
                context.BookShareTagPromotions.Remove(marker);
            }
        }

        idReplacements[duplicate.Id] = winner.Id;
        foreach (var name in nameReplacements.Where(item => item.Value.Id == duplicate.Id).Select(item => item.Key)
                     .ToArray())
        {
            nameReplacements[name] = winner;
        }

        nameReplacements[duplicate.Name] = winner;
        context.Tags.Remove(duplicate);
    }

    private async Task RewriteSnapshotsAsync(
        IReadOnlyDictionary<string, Genre> genreNames,
        IReadOnlyDictionary<Guid, Guid> tagIds,
        IReadOnlyDictionary<string, Tag> tagNames,
        CancellationToken cancellationToken)
    {
        if (genreNames.Count == 0 && tagIds.Count == 0 && tagNames.Count == 0)
        {
            return;
        }

        var snapshots = await context.PublicBookSnapshots.ToListAsync(cancellationToken);
        foreach (var snapshot in snapshots)
        {
            var genres = Deserialize<PublicBookMetadataDto[]>(snapshot.GenresJson)
                .Select(item => genreNames.TryGetValue(item.Name, out var winner)
                    ? new PublicBookMetadataDto(winner.Name, winner.Description)
                    : item)
                .GroupBy(item => MetadataNameSimilarity.CreateKey(item.Name))
                .Select(group => group.First())
                .ToArray();
            var tags = Deserialize<PublicBookMetadataDto[]>(snapshot.TagsJson)
                .Select(item => tagNames.TryGetValue(item.Name, out var winner)
                    ? new PublicBookMetadataDto(winner.Name, winner.Description)
                    : item)
                .GroupBy(item => MetadataNameSimilarity.CreateKey(item.Name))
                .Select(group => group.First())
                .ToArray();
            var publicTagIds = Deserialize<Guid[]>(snapshot.PublicTagIdsJson)
                .Select(id => ResolveReplacement(id, tagIds))
                .Distinct()
                .ToArray();

            snapshot.GenresJson = JsonSerializer.Serialize(genres, JsonOptions);
            snapshot.TagsJson = JsonSerializer.Serialize(tags, JsonOptions);
            snapshot.PublicTagIdsJson = JsonSerializer.Serialize(publicTagIds, JsonOptions);
        }
    }

    private static Guid ResolveReplacement(Guid id, IReadOnlyDictionary<Guid, Guid> replacements)
    {
        while (replacements.TryGetValue(id, out var replacement) && replacement != id)
        {
            id = replacement;
        }

        return id;
    }

    private static List<List<T>> BuildGroups<T>(IEnumerable<T> ordered, Func<T, string> name)
    {
        var groups = new List<List<T>>();
        foreach (var item in ordered)
        {
            var group = groups.FirstOrDefault(candidate =>
                MetadataNameSimilarity.IsPracticalMatch(name(candidate[0]), name(item)));
            if (group == null)
            {
                groups.Add([item]);
            }
            else
            {
                group.Add(item);
            }
        }

        return groups;
    }

    private static int DisplayNameQuality(string name)
    {
        var hasLower = name.Any(char.IsLower);
        var hasWordSeparator = name.Any(char.IsWhiteSpace);
        return (hasLower ? 2 : 0) + (hasWordSeparator ? 1 : 0);
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ??
               throw new JsonException("Invalid snapshot metadata JSON.");
    }
}
