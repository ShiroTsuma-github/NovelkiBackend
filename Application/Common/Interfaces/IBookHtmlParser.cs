namespace Application.Common.Interfaces;

using DTOs.Book;

public interface IBookHtmlParser
{
    public Task<BookHtmlParseResult> ParseAsync(string html, CancellationToken cancellationToken);
}
