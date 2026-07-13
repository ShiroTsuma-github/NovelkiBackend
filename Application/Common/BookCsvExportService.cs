namespace Application.Common;

using Application.Common.DTOs.Book;
using Application.Common.Interfaces;

public sealed class BookCsvExportService : IBookCsvExportService
{
    public string Build(IReadOnlyCollection<BookDto> books)
    {
        var rows = new List<string>
        {
            string.Join(',',
                "primaryTitle",
                "author",
                "contentType",
                "status",
                "currentChapterNumber",
                "currentChapterLabel",
                "totalChapters",
                "rating",
                "priority",
                "genres",
                "tags",
                "notes")
        };

        rows.AddRange(books.Select(book => string.Join(',',
            Escape(book.PrimaryTitle),
            Escape(book.Author),
            Escape(book.ContentType),
            Escape(book.Status),
            Escape(book.CurrentChapterNumber),
            Escape(book.CurrentChapterLabel),
            Escape(book.TotalChapters),
            Escape(book.Rating),
            Escape(book.Priority),
            Escape(string.Join("; ", book.Genres)),
            Escape(string.Join("; ", book.Tags)),
            Escape(book.Notes))));

        return string.Join(Environment.NewLine, rows) + Environment.NewLine;
    }

    private static string Escape(object? value)
    {
        var text = NeutralizeSpreadsheetFormula(value?.ToString() ?? string.Empty);
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string NeutralizeSpreadsheetFormula(string text)
    {
        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@'
            ? $"'{text}"
            : text;
    }
}
