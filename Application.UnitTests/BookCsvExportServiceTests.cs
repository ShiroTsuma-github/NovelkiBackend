using Application.Common;
using Application.Common.DTOs.Book;

namespace Application.UnitTests;

public class BookCsvExportServiceTests
{
    [Fact]
    public void Build_ShouldEscapeSpecialCharacters()
    {
        var service = new BookCsvExportService();

        var csv = service.Build([
            Book("Alpha \"Quoted\"", notes: "A,B"),
        ]);

        Assert.Contains("\"Alpha \"\"Quoted\"\"\"", csv);
        Assert.Contains("\"A,B\"", csv);
    }

    [Fact]
    public void Build_ShouldNeutralizeSpreadsheetFormulas()
    {
        var service = new BookCsvExportService();

        var csv = service.Build([
            Book("=HYPERLINK(\"https://evil.example\")", author: "+cmd", tags: ["@tag"], notes: "-payload"),
        ]);

        Assert.Contains("'=HYPERLINK", csv);
        Assert.Contains("'+cmd", csv);
        Assert.Contains("'@tag", csv);
        Assert.Contains("'-payload", csv);
    }

    private static BookDto Book(
        string title,
        string? author = null,
        IEnumerable<string>? tags = null,
        string? notes = null)
    {
        return new BookDto
        {
            Id = Guid.NewGuid(),
            Created = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            LastModified = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            PrimaryTitle = title,
            AlternativeTitles = [],
            Author = author,
            ContentType = "Novel",
            Status = "Reading",
            ProgressHistory = [],
            Genres = [],
            Tags = tags?.ToList() ?? [],
            Links = [],
            Notes = notes,
        };
    }
}
