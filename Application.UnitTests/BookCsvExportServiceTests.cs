namespace Application.UnitTests;

using Common;
using Common.DTOs.Book;

public class BookCsvExportServiceTests
{
    [Fact]
    public void Build_ShouldEscapeSpecialCharacters()
    {
        var service = new BookCsvExportService();

        var csv = service.Build([
            Book("Alpha \"Quoted\"", notes: "A,B")
        ]);

        Assert.Contains("\"Alpha \"\"Quoted\"\"\"", csv);
        Assert.Contains("\"A,B\"", csv);
    }

    [Fact]
    public void Build_ShouldNeutralizeSpreadsheetFormulas()
    {
        var service = new BookCsvExportService();

        var csv = service.Build([
            Book("=HYPERLINK(\"https://evil.example\")", "+cmd", ["@tag"], "-payload")
        ]);

        Assert.Contains("'=HYPERLINK", csv);
        Assert.Contains("'+cmd", csv);
        Assert.Contains("'@tag", csv);
        Assert.Contains("'-payload", csv);
    }

    [Fact]
    public void Build_ShouldIncludeRoundTripFieldsWithoutIdentifiers()
    {
        var book = Book("Alpha", tags: ["tag"]);
        book.AlternativeTitles = ["Alternate title"];
        book.Description = "Description";
        book.RawImportedLine = "Original line";
        book.Genres = ["Fantasy"];
        book.Links =
        [
            new BookLinkDto
            {
                Id = Guid.NewGuid(), Url = "https://example.com", SourceType = "Official", IsPrimary = true
            }
        ];
        book.ProgressHistory =
        [
            new BookProgressHistoryDto
            {
                Id = Guid.NewGuid(),
                ChangedAt = DateTimeOffset.Parse("2026-01-03T00:00:00Z"),
                ChapterNumber = 10,
                ChapterLabel = "10",
                Comment = "Read"
            }
        ];

        var csv = new BookCsvExportService().Build([book]);

        Assert.Contains("alternativeTitles", csv);
        Assert.Contains("genres", csv);
        Assert.Contains("rawImportedLine", csv);
        Assert.Contains("progressHistory", csv);
        Assert.Contains("Alternate title", csv);
        Assert.Contains("https://example.com", csv);
        Assert.DoesNotContain(book.Id.ToString(), csv);
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
            Notes = notes
        };
    }
}
