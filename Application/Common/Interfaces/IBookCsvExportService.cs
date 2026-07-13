namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookCsvExportService
{
    string Build(IReadOnlyCollection<BookDto> books);
}
