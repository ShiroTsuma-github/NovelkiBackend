namespace Infrastructure.Persistence;

using Domain.Entities;

internal static class BookQueryInclude
{
    public static IQueryable<Book> IncludeDetails(IQueryable<Book> query)
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
