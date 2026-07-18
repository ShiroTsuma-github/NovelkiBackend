namespace Application.Features.BookFeatures.Commands;

using Application.Common.DTOs.Book;
using Domain.Associations;

internal static class BookMutationSupport
{
    public static async Task EnsureBookDoesNotExistAsync(
        IBookRepository bookRepository,
        Guid ownerId,
        Guid? currentBookId,
        Guid contentTypeId,
        string primaryTitle,
        IEnumerable<BookTitleInput>? alternativeTitles,
        CancellationToken cancellationToken)
    {
        foreach (var title in EnumerateTitles(primaryTitle, alternativeTitles))
        {
            var existing = await bookRepository.GetByNameAsync(title, ownerId, contentTypeId, cancellationToken);
            if (existing != null && existing.Id != currentBookId)
            {
                throw new EntityAlreadyExistsException<Book, Guid>(title, existing.Id);
            }
        }
    }

    public static async Task<Author?> ResolveAuthorAsync(
        IAuthorRepository authorRepository,
        Guid ownerId,
        Guid? authorId,
        string? authorName,
        CancellationToken cancellationToken)
    {
        if (authorId.HasValue)
        {
            var selected = await authorRepository.GetByIdAsync(authorId.Value, cancellationToken);
            if (selected is null || (!selected.IsPublic && selected.OwnerId != ownerId))
            {
                throw new EntityNotFoundException<Author, Guid>(authorId.Value);
            }

            return selected;
        }

        if (string.IsNullOrWhiteSpace(authorName))
        {
            return null;
        }

        var normalizedAuthorName = authorName.Trim();
        var existing = await authorRepository.GetByNameAsync(ownerId, normalizedAuthorName, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var author = new Author
        {
            OwnerId = ownerId,
            IsPublic = false,
            PrimaryName = normalizedAuthorName,
            NormalizedPrimaryName = MappingExtensions.NormalizeName(normalizedAuthorName)
        };
        author.Names.Add(new AuthorName
        {
            Name = normalizedAuthorName,
            NormalizedName = MappingExtensions.NormalizeName(normalizedAuthorName),
            IsPrimary = true,
            Source = "Manual"
        });
        await authorRepository.AddAsync(author, cancellationToken);
        return author;
    }

    public static async Task<IReadOnlyCollection<Tag>> ResolveTagsAsync(
        ITagRepository tagRepository,
        Guid ownerId,
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken)
    {
        var names = tagNames
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existingTags = (await tagRepository.GetByNamesAsync(ownerId, names, cancellationToken)).ToList();
        var existingNormalized = existingTags.Select(tag => tag.NormalizedName).ToHashSet();
        foreach (var name in names)
        {
            var normalized = MappingExtensions.NormalizeName(name);
            if (existingNormalized.Contains(normalized))
            {
                continue;
            }

            var tag = new Tag { OwnerId = ownerId, Name = name, NormalizedName = normalized };
            await tagRepository.AddAsync(tag, cancellationToken);
            existingTags.Add(tag);
        }

        return existingTags;
    }

    public static List<BookTitle> BuildTitles(string primaryTitle, IEnumerable<BookTitleInput>? alternativeTitles)
    {
        var titles = new List<BookTitle> { primaryTitle.ToPrimaryTitle() };
        titles.AddRange((alternativeTitles ?? Enumerable.Empty<BookTitleInput>())
            .Where(title => !string.IsNullOrWhiteSpace(title.Title))
            .Select(title => title.ToBookTitle()));
        return titles;
    }

    public static List<BookLink> BuildLinks(IEnumerable<BookLinkInput>? links)
    {
        return (links ?? Enumerable.Empty<BookLinkInput>())
            .Select(link => link.ToBookLink())
            .ToList();
    }

    public static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IEnumerable<string> EnumerateTitles(string primaryTitle,
        IEnumerable<BookTitleInput>? alternativeTitles)
    {
        yield return primaryTitle;

        foreach (var title in alternativeTitles ?? Enumerable.Empty<BookTitleInput>())
        {
            if (!string.IsNullOrWhiteSpace(title.Title))
            {
                yield return title.Title;
            }
        }
    }
}
