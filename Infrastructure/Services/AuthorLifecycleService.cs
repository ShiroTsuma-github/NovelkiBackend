namespace Infrastructure.Services;

public sealed class AuthorLifecycleService(
    ApplicationDbContext context,
    IBookListCacheInvalidator cacheInvalidator) : IAuthorLifecycleService
{
    public async Task<Author> SetVisibilityAsync(Guid authorId, Guid actorId, bool isAdmin, bool isPublic,
        CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var author = await GetManagedAuthorAsync(authorId, actorId, isAdmin, cancellationToken);
        if (author.IsPublic == isPublic)
        {
            return author;
        }

        if (isPublic)
        {
            await EnsurePublicNamesAvailableAsync(author, cancellationToken);
            author.OwnerId ??= actorId;
            author.IsPublic = true;
        }
        else
        {
            if (author.OwnerId is null)
            {
                throw new ValidationException("A legacy public author without an owner cannot be made private.");
            }

            await LocalizeUsagesAsync(author, author.OwnerId, cancellationToken);
            author.IsPublic = false;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return author;
    }

    public async Task DeleteAsync(Guid authorId, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var author = await GetManagedAuthorAsync(authorId, actorId, isAdmin, cancellationToken);
        if (!author.IsPublic)
        {
            if (author.Books.Count > 0)
            {
                throw new EntityInUseException<Author>(author.PrimaryName);
            }
        }
        else
        {
            if (author.OwnerId.HasValue && author.Books.Any(book => book.OwnerId == author.OwnerId.Value))
            {
                throw new EntityInUseException<Author>(author.PrimaryName);
            }

            await LocalizeUsagesAsync(author, null, cancellationToken);
        }

        context.Authors.Remove(author);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<int> DeleteOwnedAuthorsAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var authors = await context.Authors
            .Include(author => author.Names)
            .Include(author => author.Books)
            .Where(author => author.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        foreach (var author in authors)
        {
            await LocalizeUsagesAsync(author, null, cancellationToken);
        }

        context.Authors.RemoveRange(authors);
        await context.SaveChangesAsync(cancellationToken);
        return authors.Count;
    }

    private async Task<Author> GetManagedAuthorAsync(Guid authorId, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        var author = await context.Authors
                         .Include(item => item.Names)
                         .Include(item => item.Books)
                         .FirstOrDefaultAsync(item => item.Id == authorId, cancellationToken)
                     ?? throw new EntityNotFoundException<Author, Guid>(authorId);
        if (author.OwnerId != actorId && !isAdmin)
        {
            throw new EntityNotFoundException<Author, Guid>(authorId);
        }

        return author;
    }

    private async Task EnsurePublicNamesAvailableAsync(Author author, CancellationToken cancellationToken)
    {
        var normalizedNames = author.Names.Select(name => name.NormalizedName)
            .Append(author.NormalizedPrimaryName)
            .Distinct()
            .ToArray();
        var conflict = await context.Authors
            .Include(item => item.Names)
            .FirstOrDefaultAsync(item => item.Id != author.Id && item.IsPublic &&
                                         (normalizedNames.Contains(item.NormalizedPrimaryName) ||
                                          item.Names.Any(name => normalizedNames.Contains(name.NormalizedName))),
                cancellationToken);
        if (conflict is not null)
        {
            throw new EntityAlreadyExistsException<Author, Guid>(conflict.PrimaryName, conflict.Id);
        }
    }

    private async Task LocalizeUsagesAsync(Author source, Guid? ownerToKeep,
        CancellationToken cancellationToken)
    {
        var affectedOwners = await context.Books
            .Where(book => book.AuthorId == source.Id && (!ownerToKeep.HasValue || book.OwnerId != ownerToKeep.Value))
            .Select(book => book.OwnerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var normalizedNames = source.Names.Select(name => name.NormalizedName)
            .Append(source.NormalizedPrimaryName)
            .Distinct()
            .ToArray();

        foreach (var ownerId in affectedOwners)
        {
            var localAuthor = await context.Authors
                .Include(author => author.Names)
                .Where(author => !author.IsPublic && author.OwnerId == ownerId)
                .OrderByDescending(author => author.NormalizedPrimaryName == source.NormalizedPrimaryName)
                .FirstOrDefaultAsync(author => normalizedNames.Contains(author.NormalizedPrimaryName) ||
                                               author.Names.Any(name => normalizedNames.Contains(name.NormalizedName)),
                    cancellationToken);
            if (localAuthor is null)
            {
                localAuthor = CreatePrivateCopy(source, ownerId);
                context.Authors.Add(localAuthor);
                await context.SaveChangesAsync(cancellationToken);
            }

            foreach (var book in source.Books.Where(book => book.OwnerId == ownerId).ToList())
            {
                book.AuthorId = localAuthor.Id;
                book.Author = localAuthor;
            }
            await cacheInvalidator.InvalidateBooksAsync(ownerId, cancellationToken);
        }
    }

    private static Author CreatePrivateCopy(Author source, Guid ownerId)
    {
        var copy = new Author
        {
            OwnerId = ownerId,
            IsPublic = false,
            PrimaryName = source.PrimaryName,
            NormalizedPrimaryName = source.NormalizedPrimaryName
        };
        foreach (var name in source.Names)
        {
            copy.Names.Add(new AuthorName
            {
                Name = name.Name,
                NormalizedName = name.NormalizedName,
                Language = name.Language,
                IsPrimary = name.IsPrimary,
                Source = name.Source
            });
        }

        return copy;
    }
}
