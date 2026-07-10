namespace Domain.Repositories;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Book?> GetForUpdateAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => GetByIdAsync(id, ownerId, cancellationToken);
    Task<Book?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => GetByIdAsync(id, cancellationToken);
    Task<Book?> GetByNameAsync(string name, Guid ownerId, Guid contentTypeId, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetAllAsync(Guid ownerId, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
        => GetAllAsync(ownerId, Skip, Take, cancellationToken);
    Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<IEnumerable<Book>> GetAllAsync(int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
        => GetAllAsync(Skip, Take, cancellationToken);
    Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> SearchAsync(Guid ownerId, BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
        => SearchAsync(ownerId, criteria, Skip, Take, cancellationToken);
    Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<IEnumerable<Book>> SearchAsync(BookSearchCriteria criteria, int Skip, int Take, string? SortBy, string? SortDirection, CancellationToken cancellationToken)
        => SearchAsync(criteria, Skip, Take, cancellationToken);
    Task<int> GetCountAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<int> GetCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<int> GetSearchCountAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken);
    Task<int> GetSearchCountAsync(BookSearchCriteria criteria, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<bool> UpdateProgressAsync(Guid id, Guid ownerId, decimal? currentChapterNumber, string? currentChapterLabel, string? comment, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<decimal?> GetTotalChaptersAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task ReplaceEditableCollectionsAsync(
        Guid bookId,
        IEnumerable<BookTitle> titles,
        IEnumerable<BookLink> links,
        IEnumerable<Guid> genreIds,
        IEnumerable<Guid> tagIds,
        BookProgressHistory? progressHistory,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    Task AddAsync(Book book, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
