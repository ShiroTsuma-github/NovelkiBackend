namespace Application.Common;

using System.Text.Json;
using DTOs.Book;

public sealed class BookCsvExportService : IBookCsvExportService
{
    private static readonly string[] Columns =
    [
        BookCsvColumns.PrimaryTitle,
        BookCsvColumns.AlternativeTitles,
        BookCsvColumns.AuthorName,
        BookCsvColumns.ContentType,
        BookCsvColumns.Status,
        BookCsvColumns.CurrentChapterNumber,
        BookCsvColumns.CurrentChapterLabel,
        BookCsvColumns.TotalChapters,
        BookCsvColumns.Rating,
        BookCsvColumns.Priority,
        BookCsvColumns.Genres,
        BookCsvColumns.Tags,
        BookCsvColumns.Description,
        BookCsvColumns.Notes,
        BookCsvColumns.RawImportedLine,
        BookCsvColumns.Links,
        BookCsvColumns.ProgressHistory
    ];

    public string Build(IReadOnlyCollection<BookDto> books)
    {
        var rows = new List<string> { string.Join(',', Columns) };

        rows.AddRange(books.Select(book => string.Join(',',
            Escape(book.PrimaryTitle),
            Escape(JsonSerializer.Serialize(book.AlternativeTitles.Select(title => new { Title = title }))),
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
            Escape(book.Description),
            Escape(book.Notes),
            Escape(book.RawImportedLine),
            Escape(JsonSerializer.Serialize(book.Links.Select(link => new
            {
                link.Url,
                link.Label,
                link.SourceType,
                link.IsPrimary,
                link.LastReadHere
            }))),
            Escape(JsonSerializer.Serialize(book.ProgressHistory.Select(history => new
            {
                history.ChangedAt, history.ChapterNumber, history.ChapterLabel, history.Comment
            }))))));

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
