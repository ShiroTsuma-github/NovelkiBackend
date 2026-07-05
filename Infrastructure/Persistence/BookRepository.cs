namespace Infrastructure.Persistence;

public class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _context;

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book?> GetByNameAsync(string name, Guid ownerId, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(name);
        return await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.OwnerId == ownerId &&
                     (b.NormalizedPrimaryTitle == normalizedName ||
                      b.Titles.Any(t => t.NormalizedTitle == normalizedName)),
                cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await GetAllAsync(ownerId, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ApplySorting(IncludeDetails(_context.Books).Where(b => b.OwnerId == ownerId), SortBy, SortDirection)
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
    {
        return await GetAllAsync(Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ApplySorting(IncludeDetails(_context.Books), SortBy, SortDirection)
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await SearchAsync(ownerId, criteria, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ApplySorting(ApplyCriteria(IncludeDetails(_context.Books).Where(b => b.OwnerId == ownerId), criteria), SortBy, SortDirection)
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken)
    {
        return await SearchAsync(criteria, Skip, Take, null, null, cancellationToken);
    }

    public async Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
    {
        return await ApplySorting(ApplyCriteria(IncludeDetails(_context.Books), criteria), SortBy, SortDirection)
            .Skip(Skip)
            .Take(Take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
        if (book != null)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(b => b.OwnerId == ownerId, cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(cancellationToken);
    }

    public Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        return ApplyCriteria(_context.Books.Where(b => b.OwnerId == ownerId), criteria)
            .CountAsync(cancellationToken);
    }

    public Task<int> GetSearchCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken)
    {
        return ApplyCriteria(_context.Books, criteria)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> UpdateProgressAsync(
        Guid id,
        Guid ownerId,
        decimal? currentChapterNumber,
        string? currentChapterLabel,
        string? comment,
        CancellationToken cancellationToken)
    {
        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, cancellationToken);
        if (book == null)
        {
            return false;
        }

        var changed = book.CurrentChapterNumber != currentChapterNumber ||
                      book.CurrentChapterLabel != currentChapterLabel;
        book.CurrentChapterNumber = currentChapterNumber;
        book.CurrentChapterLabel = currentChapterLabel;

        if (changed)
        {
            _context.BookProgressHistory.Add(new BookProgressHistory
            {
                BookId = book.Id,
                ChapterNumber = currentChapterNumber,
                ChapterLabel = currentChapterLabel,
                Comment = comment
            });
        }

        await SaveAsync(cancellationToken);
        return true;
    }

    public async Task ReplaceEditableCollectionsAsync(
        Guid bookId,
        IEnumerable<BookTitle> titles,
        IEnumerable<BookLink> links,
        IEnumerable<Guid> genreIds,
        IEnumerable<Guid> tagIds,
        BookProgressHistory? progressHistory,
        CancellationToken cancellationToken)
    {
        await _context.BookTitles.Where(t => t.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.BookLinks.Where(l => l.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookGenre>().Where(bg => bg.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookTag>().Where(bt => bt.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        DetachTrackedEditableCollections(bookId);

        _context.BookTitles.AddRange(titles.Select(t =>
        {
            t.BookId = bookId;
            return t;
        }));
        _context.BookLinks.AddRange(links.Select(l =>
        {
            l.BookId = bookId;
            return l;
        }));
        _context.Set<BookGenre>().AddRange(genreIds.Distinct().Select(genreId => new BookGenre
        {
            BookId = bookId,
            GenreId = genreId
        }));
        _context.Set<BookTag>().AddRange(tagIds.Distinct().Select(tagId => new BookTag
        {
            BookId = bookId,
            TagId = tagId
        }));

        if (progressHistory != null)
        {
            progressHistory.BookId = bookId;
            _context.BookProgressHistory.Add(progressHistory);
        }
    }

    private void DetachTrackedEditableCollections(Guid bookId)
    {
        foreach (var entry in _context.ChangeTracker.Entries<BookTitle>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookLink>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookGenre>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _context.ChangeTracker.Entries<BookTag>().Where(e => e.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
    {
        return query
            .Include(b => b.Author).ThenInclude(a => a!.Names)
            .Include(b => b.ContentType)
            .Include(b => b.Status)
            .Include(b => b.Titles)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .Include(b => b.Links)
            .Include(b => b.ProgressHistory);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static IQueryable<Book> ApplySorting(IQueryable<Book> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return NormalizeSort(sortBy) switch
        {
            "author" => descending
                ? query.OrderByDescending(b => b.Author != null ? b.Author.PrimaryName : "").ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Author != null ? b.Author.PrimaryName : "").ThenBy(b => b.PrimaryTitle),
            "status" => descending
                ? query.OrderByDescending(b => b.Status.Name).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Status.Name).ThenBy(b => b.PrimaryTitle),
            "type" => descending
                ? query.OrderByDescending(b => b.ContentType.Name).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.ContentType.Name).ThenBy(b => b.PrimaryTitle),
            "progress" => descending
                ? query.OrderByDescending(b => b.CurrentChapterNumber).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.CurrentChapterNumber).ThenBy(b => b.PrimaryTitle),
            "rating" => descending
                ? query.OrderByDescending(b => b.Rating).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Rating).ThenBy(b => b.PrimaryTitle),
            "priority" => descending
                ? query.OrderByDescending(b => b.Priority).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.Priority).ThenBy(b => b.PrimaryTitle),
            "owner" => descending
                ? query.OrderByDescending(b => b.OwnerId).ThenBy(b => b.PrimaryTitle)
                : query.OrderBy(b => b.OwnerId).ThenBy(b => b.PrimaryTitle),
            _ => descending
                ? query.OrderByDescending(b => b.PrimaryTitle)
                : query.OrderBy(b => b.PrimaryTitle)
        };
    }

    private static string NormalizeSort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "title" or "primarytitle" => "title",
            "author" => "author",
            "status" => "status",
            "type" or "contenttype" => "type",
            "progress" or "currentchapter" => "progress",
            "rating" => "rating",
            "priority" => "priority",
            "owner" or "ownerid" => "owner",
            _ => "title"
        };
    }

    private static IQueryable<Book> ApplyCriteria(IQueryable<Book> query, BookSearchCriteria criteria)
    {
        foreach (var term in criteria.Terms)
        {
            var normalized = Normalize(term);
            query = query.Where(b =>
                b.NormalizedPrimaryTitle.Contains(normalized) ||
                b.Titles.Any(t => t.NormalizedTitle.Contains(normalized)) ||
                (b.Author != null && (
                    b.Author.NormalizedPrimaryName.Contains(normalized) ||
                    b.Author.Names.Any(n => n.NormalizedName.Contains(normalized)))));
        }

        foreach (var filter in criteria.Fields)
        {
            var normalized = Normalize(filter.Value);
            query = filter.Field switch
            {
                BookSearchField.Title => query.Where(b =>
                    b.NormalizedPrimaryTitle.Contains(normalized) ||
                    b.Titles.Any(t => t.NormalizedTitle.Contains(normalized))),
                BookSearchField.Author => query.Where(b => b.Author != null && (
                    b.Author.NormalizedPrimaryName.Contains(normalized) ||
                    b.Author.Names.Any(n => n.NormalizedName.Contains(normalized)))),
                BookSearchField.Tag => query.Where(b => b.BookTags.Any(bt => bt.Tag.NormalizedName.Contains(normalized))),
                BookSearchField.Genre => query.Where(b => b.BookGenres.Any(bg => bg.Genre.NormalizedName.Contains(normalized))),
                BookSearchField.Status => query.Where(b => b.Status.Name.ToUpper().Contains(normalized)),
                BookSearchField.Type => query.Where(b => b.ContentType.Name.ToUpper().Contains(normalized)),
                _ => query
            };
        }

        foreach (var filter in criteria.Numbers)
        {
            query = filter.Field switch
            {
                BookSearchNumberField.Rating => ApplyRating(query, filter.Operator, filter.Value),
                BookSearchNumberField.Priority => ApplyPriority(query, filter.Operator, filter.Value),
                BookSearchNumberField.CurrentChapter => ApplyCurrentChapter(query, filter.Operator, filter.Value),
                BookSearchNumberField.TotalChapters => ApplyTotalChapters(query, filter.Operator, filter.Value),
                _ => query
            };
        }

        return query;
    }

    private static IQueryable<Book> ApplyRating(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.Rating != null && (decimal)b.Rating.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.Rating != null && (decimal)b.Rating.Value >= value),
            BookSearchOperator.LessThan => query.Where(b => b.Rating != null && (decimal)b.Rating.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.Rating != null && (decimal)b.Rating.Value <= value),
            _ => query.Where(b => b.Rating != null && (decimal)b.Rating.Value == value)
        };
    }

    private static IQueryable<Book> ApplyPriority(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.Priority != null && (decimal)b.Priority.Value > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.Priority != null && (decimal)b.Priority.Value >= value),
            BookSearchOperator.LessThan => query.Where(b => b.Priority != null && (decimal)b.Priority.Value < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.Priority != null && (decimal)b.Priority.Value <= value),
            _ => query.Where(b => b.Priority != null && (decimal)b.Priority.Value == value)
        };
    }

    private static IQueryable<Book> ApplyCurrentChapter(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber >= value),
            BookSearchOperator.LessThan => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber <= value),
            _ => query.Where(b => b.CurrentChapterNumber != null && b.CurrentChapterNumber == value)
        };
    }

    private static IQueryable<Book> ApplyTotalChapters(IQueryable<Book> query, BookSearchOperator op, decimal value)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => query.Where(b => b.TotalChapters != null && b.TotalChapters > value),
            BookSearchOperator.GreaterThanOrEqual => query.Where(b => b.TotalChapters != null && b.TotalChapters >= value),
            BookSearchOperator.LessThan => query.Where(b => b.TotalChapters != null && b.TotalChapters < value),
            BookSearchOperator.LessThanOrEqual => query.Where(b => b.TotalChapters != null && b.TotalChapters <= value),
            _ => query.Where(b => b.TotalChapters != null && b.TotalChapters == value)
        };
    }
}
