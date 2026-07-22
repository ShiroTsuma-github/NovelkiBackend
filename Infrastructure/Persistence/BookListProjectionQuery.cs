namespace Infrastructure.Persistence;

using Application.Common.DTOs.Book;
using Domain.Entities;

public sealed class BookListProjectionQuery
{
    private readonly ApplicationDbContext _context;
    private readonly BookSortBuilder _sortBuilder;

    public BookListProjectionQuery(ApplicationDbContext context, BookSortBuilder sortBuilder)
    {
        _context = context;
        _sortBuilder = sortBuilder;
    }

    public async Task<IReadOnlyCollection<BookListItemDto>> GetBooksAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (_sortBuilder.ShouldSortDateOnClient(sortBy))
        {
            var clientSortedPage = BookSortBuilder
                .SortProjectedByDate(await ProjectBooks(query).ToListAsync(cancellationToken), sortBy, sortDirection)
                .Skip(skip)
                .Take(take)
                .ToList();

            return clientSortedPage.Select(BookListProjectionMapper.MapListProjection).ToList();
        }

        var pageQuery =
            await BuildProjectedPageQueryAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var page = await pageQuery.ToListAsync(cancellationToken);

        return page.Select(BookListProjectionMapper.MapListProjection).ToList();
    }

    public async Task<IReadOnlyCollection<AdminBookListItemDto>> GetAdminBooksAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        if (_sortBuilder.ShouldSortDateOnClient(sortBy))
        {
            var sortedPage = BookSortBuilder
                .SortProjectedByDate(await ProjectBooks(query).ToListAsync(cancellationToken), sortBy, sortDirection)
                .Skip(skip)
                .Take(take)
                .ToList();
            var clientSortedOwners =
                await GetOwnersAsync(sortedPage.Select(book => book.OwnerId), cancellationToken);

            return sortedPage.Select(book => BookListProjectionMapper.MapAdminListProjection(book, clientSortedOwners))
                .ToList();
        }

        var pageQuery =
            await BuildProjectedPageQueryAsync(query, skip, take, sortBy, sortDirection, cancellationToken);
        var page = await pageQuery.ToListAsync(cancellationToken);
        var owners =
            await GetOwnersAsync(page.Select(book => book.OwnerId), cancellationToken);

        return page.Select(book => BookListProjectionMapper.MapAdminListProjection(book, owners)).ToList();
    }

    private async Task<Dictionary<Guid, BookOwnerProjection>> GetOwnersAsync(IEnumerable<Guid> ownerIds,
        CancellationToken cancellationToken)
    {
        var distinctOwnerIds = ownerIds.Distinct().ToArray();
        return await _context.Users
            .AsNoTracking()
            .Where(user => distinctOwnerIds.Contains(user.Id))
            .Select(user => new BookOwnerProjection { Id = user.Id, Username = user.UserName, Email = user.Email })
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    private async Task<IQueryable<BookListProjection>> BuildProjectedPageQueryAsync(
        IQueryable<Book> query,
        int skip,
        int take,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var sortedPage =
            (await _sortBuilder.ApplySortingAsync(query, sortBy, sortDirection, cancellationToken))
            .Skip(skip)
            .Take(take);
        return ProjectBooks(sortedPage);
    }

    private static IQueryable<BookListProjection> ProjectBooks(IQueryable<Book> query)
    {
        return query
            .Select(book => new BookListProjection
            {
                Id = book.Id,
                Created = book.Created,
                LastModified = book.LastModified,
                OwnerId = book.OwnerId,
                PrimaryTitle = book.PrimaryTitle,
                Description = book.Description,
                AlternativeTitles = book.Titles
                    .Where(title => !title.IsPrimary)
                    .OrderBy(title => title.Id)
                    .Select(title => title.Title)
                    .ToList(),
                AlternativeTitlesCount = book.Titles.Count(title => !title.IsPrimary),
                Author = book.Author != null ? book.Author.PrimaryName : null,
                AuthorOtherNames = book.Author != null
                    ? book.Author.Names
                        .Where(name => !name.IsPrimary)
                        .OrderBy(name => name.Name)
                        .Select(name => name.Name)
                        .ToList()
                    : new List<string>(),
                ContentType = book.ContentType.Name,
                Status = book.Status.Name,
                CurrentChapterNumber = book.CurrentChapterNumber,
                CurrentChapterLabel = book.CurrentChapterLabel,
                TotalChapters = book.TotalChapters,
                Rating = book.Rating,
                Priority = book.Priority,
                Notes = book.Notes,
                Genres = book.BookGenres
                    .OrderBy(bookGenre => bookGenre.Genre.Name)
                    .Select(bookGenre => bookGenre.Genre.Name)
                    .ToList(),
                GenreDescriptions = book.BookGenres
                    .OrderBy(bookGenre => bookGenre.Genre.Name)
                    .Select(bookGenre => bookGenre.Genre.Description)
                    .ToList(),
                GenresCount = book.BookGenres.Count(),
                Tags = book.BookTags
                    .OrderBy(bookTag => bookTag.Tag.Name)
                    .Select(bookTag => bookTag.Tag.Name)
                    .ToList(),
                TagDescriptions = book.BookTags
                    .OrderBy(bookTag => bookTag.Tag.Name)
                    .Select(bookTag => bookTag.Tag.Description)
                    .ToList(),
                TagsCount = book.BookTags.Count(),
                LinksCount = book.Links.Count(),
                CoverStatus = book.Cover != null ? book.Cover.Status : null,
                CoverSource = book.Cover != null ? book.Cover.Source : null,
                CoverFailureReason = book.Cover != null ? book.Cover.FailureReason : null,
                CoverLastAttemptAt = book.Cover != null ? book.Cover.LastAttemptAt : null,
                CoverLastModified = book.Cover != null ? book.Cover.LastModified : null,
                CoverCreated = book.Cover != null ? book.Cover.Created : null,
                HasCoverStoragePath = book.Cover != null && book.Cover.StoragePath != null,
                HasCoverThumbnailStoragePath = book.Cover != null && book.Cover.ThumbnailStoragePath != null
            });
    }
}
