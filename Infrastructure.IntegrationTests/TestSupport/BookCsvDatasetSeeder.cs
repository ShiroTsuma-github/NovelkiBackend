using System.Globalization;
using System.Runtime.CompilerServices;
using Application.Common;
using Domain.Associations;
using Domain.Entities;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace Infrastructure.IntegrationTests.TestSupport;

public static class BookCsvDatasetSeeder
{
    private static readonly string[] TypeNames = ["Novel", "Manga", "Manhwa", "Manhua", "Other"];

    private static readonly string[] StatusNames =
        ["Reading", "Completed", "Plan To Read", "On Hold", "Dropped", "Unknown"];

    private static readonly string[] FallbackGenreNames =
        ["Fantasy", "Action", "Drama", "Adventure", "Romance", "Comedy", "Xianxia", "Harem"];

    private static readonly string[] CsvRelativePaths =
        ["Sample/books-export.csv", "Infrastructure.IntegrationTests/Sample/books-export.csv"];

    public static async Task<BookCsvDatasetSnapshot> SeedAsync(
        ApplicationDbContext context,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var rows = ReadRows().ToList();
        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                "The local CSV test dataset notes/books-export.csv contains no importable books.");
        }

        Dictionary<string, ContentType> contentTypes = await context.ContentTypes
            .Where(type => TypeNames.Contains(type.Name))
            .ToDictionaryAsync(type => type.Name, cancellationToken);
        Dictionary<string, Status> statuses = await context.Statuses
            .Where(status => StatusNames.Contains(status.Name))
            .ToDictionaryAsync(status => status.Name, cancellationToken);

        string[] genreNames = rows
            .SelectMany(row => row.Genres)
            .Concat(FallbackGenreNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Dictionary<string, Genre> genres = await EnsureGenresAsync(context, genreNames, cancellationToken);
        Dictionary<string, Tag> tags =
            await EnsureTagsAsync(context, ownerId, rows.SelectMany(row => row.Tags), cancellationToken);
        Dictionary<string, Author> authors =
            await EnsureAuthorsAsync(context, rows.Select(row => row.Author), cancellationToken);

        var random = new Random(1337);
        var usedKeys = new HashSet<BookUniqueKey>();
        var samples = new List<BookCsvDatasetSample>();
        var typeCounts = TypeNames.ToDictionary(name => name, _ => 0);
        var statusCounts = StatusNames.ToDictionary(name => name, _ => 0);
        int booksWithGenres = 0;
        int preservedTaggedBooks = 0;
        int preservedRatedBooks = 0;
        int preservedProgressBooks = 0;

        for (int index = 0; index < rows.Count; index++)
        {
            CsvBookRow row = rows[index];
            string typeName = TypeNames[index % TypeNames.Length];
            string statusName = StatusNames[index % StatusNames.Length];
            string title = MakeUniqueTitle(row.PrimaryTitle, ownerId, contentTypes[typeName].Id, usedKeys);
            var book = new Book
            {
                OwnerId = ownerId,
                PrimaryTitle = title,
                NormalizedPrimaryTitle = MappingExtensions.NormalizeName(title),
                Author = row.Author == null ? null : authors[MappingExtensions.NormalizeName(row.Author)],
                AuthorId = row.Author == null ? null : authors[MappingExtensions.NormalizeName(row.Author)].Id,
                ContentTypeId = contentTypes[typeName].Id,
                StatusId = statuses[statusName].Id,
                CurrentChapterNumber = row.CurrentChapterNumber,
                CurrentChapterLabel = row.CurrentChapterLabel,
                TotalChapters = row.TotalChapters,
                Rating = row.Rating,
                Priority = row.Priority,
                Notes = row.Notes,
                Cover = new BookCover { Status = BookCoverStatus.Pending }
            };

            book.Titles.Add(new BookTitle
            {
                Title = title,
                NormalizedTitle = MappingExtensions.NormalizeName(title),
                IsPrimary = true,
                Source = "CsvTestDataset"
            });

            foreach (string genreName in SelectGenres(row, genreNames, random))
            {
                book.BookGenres.Add(new BookGenre
                {
                    Book = book, Genre = genres[MappingExtensions.NormalizeName(genreName)]
                });
            }

            foreach (string tagName in row.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                book.BookTags.Add(new BookTag { Book = book, Tag = tags[MappingExtensions.NormalizeName(tagName)] });
            }

            if (row.CurrentChapterNumber != null || row.CurrentChapterLabel != null)
            {
                book.ProgressHistory.Add(new BookProgressHistory
                {
                    ChapterNumber = row.CurrentChapterNumber,
                    ChapterLabel =
                        row.CurrentChapterLabel ?? row.CurrentChapterNumber?.ToString(CultureInfo.InvariantCulture),
                    ChangedAt = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00", CultureInfo.InvariantCulture)
                        .AddMinutes(index)
                });
            }

            context.Books.Add(book);
            typeCounts[typeName]++;
            statusCounts[statusName]++;
            if (book.BookGenres.Count > 0)
            {
                booksWithGenres++;
            }

            if (row.Tags.Count > 0 && book.BookTags.Count > 0)
            {
                preservedTaggedBooks++;
            }

            if (row.Rating != null)
            {
                preservedRatedBooks++;
            }

            if (row.CurrentChapterNumber != null)
            {
                preservedProgressBooks++;
            }

            samples.Add(BookCsvDatasetSample.From(book, typeName, statusName));
        }

        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        return new BookCsvDatasetSnapshot(
            rows.Count,
            typeCounts,
            statusCounts,
            genreNames,
            booksWithGenres,
            preservedTaggedBooks,
            preservedRatedBooks,
            preservedProgressBooks,
            samples);
    }

    public static void AssertBalancedTypeDistribution(BookCsvDatasetSnapshot snapshot)
    {
        AssertBalanced(snapshot.TypeCounts);
    }

    public static void AssertBalancedStatusDistribution(BookCsvDatasetSnapshot snapshot)
    {
        AssertBalanced(snapshot.StatusCounts);
    }

    private static void AssertBalanced(IReadOnlyDictionary<string, int> counts)
    {
        Assert.NotEmpty(counts);
        Assert.True(counts.Values.Max() - counts.Values.Min() <= 1,
            $"Expected balanced distribution, got {string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}"))}.");
    }

    private static async Task<Dictionary<string, Genre>> EnsureGenresAsync(
        ApplicationDbContext context,
        IEnumerable<string> genreNames,
        CancellationToken cancellationToken)
    {
        string[] names = genreNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        Dictionary<string, Genre> genres = await context.Genres
            .Where(genre => normalizedNames.Contains(genre.NormalizedName))
            .ToDictionaryAsync(genre => genre.NormalizedName, cancellationToken);

        foreach (string name in names)
        {
            string normalizedName = MappingExtensions.NormalizeName(name);
            if (genres.ContainsKey(normalizedName))
            {
                continue;
            }

            Genre genre = TestData.Genre(name);
            context.Genres.Add(genre);
            genres[normalizedName] = genre;
        }

        return genres;
    }

    private static async Task<Dictionary<string, Tag>> EnsureTagsAsync(
        ApplicationDbContext context,
        Guid ownerId,
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken)
    {
        string[] names = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        Dictionary<string, Tag> tags = await context.Tags
            .Where(tag => tag.OwnerId == ownerId && normalizedNames.Contains(tag.NormalizedName))
            .ToDictionaryAsync(tag => tag.NormalizedName, cancellationToken);

        foreach (string name in names)
        {
            string normalizedName = MappingExtensions.NormalizeName(name);
            if (tags.ContainsKey(normalizedName))
            {
                continue;
            }

            Tag tag = TestData.Tag(ownerId, name);
            context.Tags.Add(tag);
            tags[normalizedName] = tag;
        }

        return tags;
    }

    private static async Task<Dictionary<string, Author>> EnsureAuthorsAsync(
        ApplicationDbContext context,
        IEnumerable<string?> authorNames,
        CancellationToken cancellationToken)
    {
        string[] names = authorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        Dictionary<string, Author> authors = await context.Authors
            .Where(author => normalizedNames.Contains(author.NormalizedPrimaryName))
            .ToDictionaryAsync(author => author.NormalizedPrimaryName, cancellationToken);

        foreach (string name in names)
        {
            string normalizedName = MappingExtensions.NormalizeName(name);
            if (authors.ContainsKey(normalizedName))
            {
                continue;
            }

            Author author = TestData.Author(name);
            context.Authors.Add(author);
            authors[normalizedName] = author;
        }

        return authors;
    }

    private static IReadOnlyCollection<string> SelectGenres(CsvBookRow row, IReadOnlyList<string> genreNames,
        Random random)
    {
        var selected = new HashSet<string>(row.Genres, StringComparer.OrdinalIgnoreCase);
        int targetCount = Math.Max(selected.Count, random.Next(1, Math.Min(3, genreNames.Count) + 1));
        while (selected.Count < targetCount)
        {
            selected.Add(genreNames[random.Next(genreNames.Count)]);
        }

        return selected;
    }

    private static string MakeUniqueTitle(string baseTitle, Guid ownerId, Guid contentTypeId,
        HashSet<BookUniqueKey> usedKeys)
    {
        string title = baseTitle;
        int suffix = 2;
        while (!usedKeys.Add(new BookUniqueKey(ownerId, MappingExtensions.NormalizeName(title), contentTypeId)))
        {
            title = $"{baseTitle} ({suffix})";
            suffix++;
        }

        return title;
    }

    private static IEnumerable<CsvBookRow> ReadRows()
    {
        string csvPath = FindCsvPath();
        using var parser = new TextFieldParser(csvPath)
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        string[] headers = parser.ReadFields() ?? [];
        var headerIndexes = headers
            .Select((header, index) => new { Header = header, Index = index })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        while (!parser.EndOfData)
        {
            string[]? fields = parser.ReadFields();
            if (fields == null)
            {
                continue;
            }

            string? title = GetField(fields, headerIndexes, "primaryTitle");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            yield return new CsvBookRow(
                title.Trim(),
                TrimToNull(GetField(fields, headerIndexes, "author")),
                TryDecimal(GetField(fields, headerIndexes, "currentChapterNumber")),
                TrimToNull(GetField(fields, headerIndexes, "currentChapterLabel")),
                TryDecimal(GetField(fields, headerIndexes, "totalChapters")),
                TryInt(GetField(fields, headerIndexes, "rating")),
                TryInt(GetField(fields, headerIndexes, "priority")),
                SplitSemicolonList(GetField(fields, headerIndexes, "genres")),
                SplitSemicolonList(GetField(fields, headerIndexes, "tags")),
                TrimToNull(GetField(fields, headerIndexes, "notes")));
        }
    }

    private static string FindCsvPath([CallerFilePath] string sourceFilePath = "")
    {
        foreach (string? startPath in new[]
                 {
                     AppContext.BaseDirectory, Directory.GetCurrentDirectory(),
                     Path.GetDirectoryName(sourceFilePath)
                 })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                foreach (string relativePath in CsvRelativePaths)
                {
                    string candidate = Path.Combine(current.FullName, relativePath);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException(
            "Expected CSV test dataset at Infrastructure.IntegrationTests/Sample/books-export.csv or copied build output Sample/books-export.csv.");
    }

    private static string? GetField(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> headerIndexes,
        string header)
    {
        return headerIndexes.TryGetValue(header, out int index) && index < fields.Count ? fields[index] : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal? TryDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : null;
    }

    private static int? TryInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : null;
    }

    private static IReadOnlyList<string> SplitSemicolonList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private sealed record CsvBookRow(
        string PrimaryTitle,
        string? Author,
        decimal? CurrentChapterNumber,
        string? CurrentChapterLabel,
        decimal? TotalChapters,
        int? Rating,
        int? Priority,
        IReadOnlyList<string> Genres,
        IReadOnlyList<string> Tags,
        string? Notes);

    private sealed record BookUniqueKey(Guid OwnerId, string NormalizedTitle, Guid ContentTypeId);
}

public sealed record BookCsvDatasetSnapshot(
    int BookCount,
    IReadOnlyDictionary<string, int> TypeCounts,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyList<string> GenreNames,
    int BooksWithGenres,
    int PreservedTaggedBooks,
    int PreservedRatedBooks,
    int PreservedProgressBooks,
    IReadOnlyList<BookCsvDatasetSample> Samples)
{
    public BookCsvDatasetSample Any => Samples[0];
    public BookCsvDatasetSample WithTag => Samples.First(sample => sample.Tags.Count > 0);
    public BookCsvDatasetSample WithRating => Samples.First(sample => sample.Rating != null);

    public BookCsvDatasetSample WithTagAndRating =>
        Samples.First(sample => sample.Tags.Count > 0 && sample.Rating != null);

    public BookCsvDatasetSample WithTotalChapters => Samples.First(sample => sample.TotalChapters != null);
    public BookCsvDatasetSample WithNotes => Samples.First(sample => !string.IsNullOrWhiteSpace(sample.Notes));
}

public sealed record BookCsvDatasetSample(
    Guid Id,
    string PrimaryTitle,
    string ContentType,
    string Status,
    decimal? CurrentChapterNumber,
    string? CurrentChapterLabel,
    decimal? TotalChapters,
    int? Rating,
    int? Priority,
    string? Notes,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags)
{
    public static BookCsvDatasetSample From(Book book, string contentType, string status)
    {
        return new BookCsvDatasetSample(
            book.Id,
            book.PrimaryTitle,
            contentType,
            status,
            book.CurrentChapterNumber,
            book.CurrentChapterLabel,
            book.TotalChapters,
            book.Rating,
            book.Priority,
            book.Notes,
            book.BookGenres.Select(bookGenre => bookGenre.Genre.Name).ToArray(),
            book.BookTags.Select(bookTag => bookTag.Tag.Name).ToArray());
    }
}
