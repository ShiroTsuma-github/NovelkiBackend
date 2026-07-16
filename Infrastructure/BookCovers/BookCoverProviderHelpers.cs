namespace Infrastructure.BookCovers;

internal static class BookCoverProviderHelpers
{
    public static IEnumerable<string> EnumerateTitles(Book book)
    {
        yield return book.PrimaryTitle;

        foreach (var title in book.Titles.Where(t => !t.IsPrimary).Select(t => t.Title))
        {
            yield return title;
        }
    }
}
