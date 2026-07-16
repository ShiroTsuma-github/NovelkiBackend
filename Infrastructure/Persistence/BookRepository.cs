namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
            .FirstOrDefaultAsync(book => book.Id == id && book.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await IncludeDetails(_context.Books)
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(book => book.Id == id && book.OwnerId == ownerId, cancellationToken);
    }

    public async Task<Book?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<Book?> GetByNameAsync(string name, Guid ownerId, Guid contentTypeId,
        CancellationToken cancellationToken)
    {
        string normalizedName = MappingExtensions.NormalizeName(name);
        return await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(
                book => book.OwnerId == ownerId &&
                        book.ContentTypeId == contentTypeId &&
                        (book.NormalizedPrimaryTitle == normalizedName ||
                         book.Titles.Any(title => title.NormalizedTitle == normalizedName)),
                cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        Book? book =
            await _context.Books.FirstOrDefaultAsync(book => book.Id == id && book.OwnerId == ownerId,
                cancellationToken);
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
        return _context.Books.CountAsync(book => book.OwnerId == ownerId, cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return _context.Books.CountAsync(cancellationToken);
    }

    public async Task<bool> UpdateProgressAsync(
        Guid id,
        Guid ownerId,
        decimal? currentChapterNumber,
        string? currentChapterLabel,
        string? comment,
        CancellationToken cancellationToken)
    {
        Book? book = await _context.Books
            .FirstOrDefaultAsync(book => book.Id == id && book.OwnerId == ownerId, cancellationToken);
        if (book == null)
        {
            return false;
        }

        bool progressChanged = book.CurrentChapterNumber != currentChapterNumber ||
                               book.CurrentChapterLabel != currentChapterLabel;
        bool hasComment = !string.IsNullOrWhiteSpace(comment);
        if (book.TotalChapters.HasValue && book.TotalChapters > 0 && currentChapterNumber.HasValue &&
            currentChapterNumber > book.TotalChapters)
        {
            throw new ValidationException("Current chapter cannot be greater than total chapters.");
        }

        book.CurrentChapterNumber = currentChapterNumber;
        book.CurrentChapterLabel = currentChapterLabel;

        if (progressChanged || hasComment)
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

    public async Task<decimal?> GetTotalChaptersAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Where(book => book.Id == id && book.OwnerId == ownerId)
            .Select(book => book.TotalChapters)
            .FirstOrDefaultAsync(cancellationToken);
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
        await _context.BookTitles.Where(title => title.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.BookLinks.Where(link => link.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookGenre>().Where(bookGenre => bookGenre.BookId == bookId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.Set<BookTag>().Where(bookTag => bookTag.BookId == bookId).ExecuteDeleteAsync(cancellationToken);
        DetachTrackedEditableCollections(bookId);

        _context.BookTitles.AddRange(titles.Select(title =>
        {
            title.BookId = bookId;
            return title;
        }));
        _context.BookLinks.AddRange(links.Select(link =>
        {
            link.BookId = bookId;
            return link;
        }));
        _context.Set<BookGenre>().AddRange(genreIds.Distinct().Select(genreId => new BookGenre
        {
            BookId = bookId, GenreId = genreId
        }));
        _context.Set<BookTag>().AddRange(tagIds.Distinct().Select(tagId => new BookTag
        {
            BookId = bookId, TagId = tagId
        }));

        if (progressHistory != null)
        {
            progressHistory.BookId = bookId;
            _context.BookProgressHistory.Add(progressHistory);
        }
    }

    private void DetachTrackedEditableCollections(Guid bookId)
    {
        foreach (EntityEntry<BookTitle> entry in _context.ChangeTracker.Entries<BookTitle>()
                     .Where(entry => entry.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (EntityEntry<BookLink> entry in _context.ChangeTracker.Entries<BookLink>()
                     .Where(entry => entry.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (EntityEntry<BookGenre> entry in _context.ChangeTracker.Entries<BookGenre>()
                     .Where(entry => entry.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (EntityEntry<BookTag> entry in _context.ChangeTracker.Entries<BookTag>()
                     .Where(entry => entry.Entity.BookId == bookId).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
    {
        return query
            .Include(book => book.Author).ThenInclude(author => author!.Names)
            .Include(book => book.Cover)
            .Include(book => book.ContentType)
            .Include(book => book.Status)
            .Include(book => book.Titles)
            .Include(book => book.BookGenres).ThenInclude(bookGenre => bookGenre.Genre)
            .Include(book => book.BookTags).ThenInclude(bookTag => bookTag.Tag)
            .Include(book => book.Links)
            .Include(book => book.ProgressHistory);
    }
}
