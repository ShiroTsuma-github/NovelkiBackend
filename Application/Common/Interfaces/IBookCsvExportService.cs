namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookCsvExportService
{
    public string Build(IReadOnlyCollection<BookDto> books);
}
