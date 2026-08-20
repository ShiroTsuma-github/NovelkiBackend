using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<BookCsvDatasetSnapshot> SeedAsync(
        ApplicationDbContext context,
        Guid ownerId,
        CancellationToken cancellationToken = default,
        string? csvPath = null,
        BookCsvSeedProfile profile = BookCsvSeedProfile.SyntheticBalanced)
    {
        var rows = ReadRows(csvPath).ToList();
        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                "The local CSV test dataset notes/books-export.csv contains no importable books.");
        }

        var contentTypes = await context.ContentTypes
            .Where(type => TypeNames.Contains(type.Name))
            .ToDictionaryAsync(type => type.Name, cancellationToken);
        var statuses = await context.Statuses
            .Where(status => StatusNames.Contains(status.Name))
            .ToDictionaryAsync(status => status.Name, cancellationToken);

        var genreNames = rows
            .SelectMany(row => row.Genres)
            .Concat(FallbackGenreNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var genres = await EnsureGenresAsync(context, genreNames, cancellationToken);
        var tags =
            await EnsureTagsAsync(context, ownerId, rows.SelectMany(row => row.Tags), cancellationToken);
        var authors =
            await EnsureAuthorsAsync(context, rows.Select(row => row.Author), cancellationToken);

        var random = new Random(1337);
        var usedKeys = new HashSet<BookUniqueKey>();
        var samples = new List<BookCsvDatasetSample>();
        var typeCounts = TypeNames.ToDictionary(name => name, _ => 0);
        var statusCounts = StatusNames.ToDictionary(name => name, _ => 0);
        var booksWithGenres = 0;
        var preservedTaggedBooks = 0;
        var preservedRatedBooks = 0;
        var preservedProgressBooks = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var typeName = profile == BookCsvSeedProfile.PreserveSource
                ? ResolveSourceName(row.ContentType, contentTypes, "Other")
                : TypeNames[index % TypeNames.Length];
            var statusName = profile == BookCsvSeedProfile.PreserveSource
                ? ResolveSourceName(row.Status, statuses, "Unknown")
                : StatusNames[index % StatusNames.Length];
            var title = MakeUniqueTitle(row.PrimaryTitle, ownerId, contentTypes[typeName].Id, usedKeys);
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
                Description = row.Description,
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

            var normalizedTitles = new HashSet<string>(StringComparer.Ordinal)
            {
                MappingExtensions.NormalizeName(title)
            };
            foreach (var alternativeTitle in row.AlternativeTitles)
            {
                var normalizedAlternativeTitle = MappingExtensions.NormalizeName(alternativeTitle);
                if (!normalizedTitles.Add(normalizedAlternativeTitle))
                {
                    continue;
                }

                book.Titles.Add(new BookTitle
                {
                    Title = alternativeTitle,
                    NormalizedTitle = normalizedAlternativeTitle,
                    IsPrimary = false,
                    Source = "CsvTestDataset"
                });
            }

            foreach (var genreName in SelectGenres(row, genreNames, random, profile))
            {
                book.BookGenres.Add(new BookGenre
                {
                    Book = book, Genre = genres[MappingExtensions.NormalizeName(genreName)]
                });
            }

            foreach (var tagName in row.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                book.BookTags.Add(new BookTag { Book = book, Tag = tags[MappingExtensions.NormalizeName(tagName)] });
            }

            if (profile == BookCsvSeedProfile.PreserveSource)
            {
                foreach (var link in row.Links)
                {
                    book.Links.Add(new BookLink
                    {
                        Url = link.Url,
                        Label = link.Label,
                        SourceType = link.SourceType,
                        IsPrimary = link.IsPrimary,
                        LastReadHere = link.LastReadHere
                    });
                }

                foreach (var progress in row.ProgressHistory)
                {
                    book.ProgressHistory.Add(new BookProgressHistory
                    {
                        ChangedAt = progress.ChangedAt,
                        ChapterNumber = progress.ChapterNumber,
                        ChapterLabel = progress.ChapterLabel,
                        Comment = progress.Comment
                    });
                }

                if (row.ProgressHistory.Count > 0)
                {
                    book.LastProgressUpdatedAt = row.ProgressHistory.Max(progress => progress.ChangedAt);
                }
            }
            else if (row.CurrentChapterNumber != null || row.CurrentChapterLabel != null)
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
        var names = genreNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        var genres = await context.Genres
            .Where(genre => normalizedNames.Contains(genre.NormalizedName))
            .ToDictionaryAsync(genre => genre.NormalizedName, cancellationToken);

        foreach (var name in names)
        {
            var normalizedName = MappingExtensions.NormalizeName(name);
            if (genres.ContainsKey(normalizedName))
            {
                continue;
            }

            var genre = TestData.Genre(name);
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
        var names = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        var tags = await context.Tags
            .Where(tag => tag.OwnerId == ownerId && normalizedNames.Contains(tag.NormalizedName))
            .ToDictionaryAsync(tag => tag.NormalizedName, cancellationToken);

        foreach (var name in names)
        {
            var normalizedName = MappingExtensions.NormalizeName(name);
            if (tags.ContainsKey(normalizedName))
            {
                continue;
            }

            var tag = TestData.Tag(ownerId, name);
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
        var names = authorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedNames = names.Select(MappingExtensions.NormalizeName).ToArray();
        var authors = await context.Authors
            .Where(author => normalizedNames.Contains(author.NormalizedPrimaryName))
            .ToDictionaryAsync(author => author.NormalizedPrimaryName, cancellationToken);

        foreach (var name in names)
        {
            var normalizedName = MappingExtensions.NormalizeName(name);
            if (authors.ContainsKey(normalizedName))
            {
                continue;
            }

            var author = TestData.Author(name);
            context.Authors.Add(author);
            authors[normalizedName] = author;
        }

        return authors;
    }

    private static string ResolveSourceName<T>(string? sourceName, IReadOnlyDictionary<string, T> knownNames,
        string fallback)
    {
        return sourceName != null && knownNames.ContainsKey(sourceName) ? sourceName : fallback;
    }

    private static IReadOnlyCollection<string> SelectGenres(CsvBookRow row, IReadOnlyList<string> genreNames,
        Random random, BookCsvSeedProfile profile)
    {
        var selected = new HashSet<string>(row.Genres, StringComparer.OrdinalIgnoreCase);
        if (profile == BookCsvSeedProfile.PreserveSource)
        {
            return selected;
        }

        var targetCount = Math.Max(selected.Count, random.Next(1, Math.Min(3, genreNames.Count) + 1));
        while (selected.Count < targetCount)
        {
            selected.Add(genreNames[random.Next(genreNames.Count)]);
        }

        return selected;
    }

    private static string MakeUniqueTitle(string baseTitle, Guid ownerId, Guid contentTypeId,
        HashSet<BookUniqueKey> usedKeys)
    {
        var title = baseTitle;
        var suffix = 2;
        while (!usedKeys.Add(new BookUniqueKey(ownerId, MappingExtensions.NormalizeName(title), contentTypeId)))
        {
            title = $"{baseTitle} ({suffix})";
            suffix++;
        }

        return title;
    }

    private static IEnumerable<CsvBookRow> ReadRows(string? csvPath = null)
    {
        var resolvedCsvPath = FindCsvPath(csvPath);
        using var parser = new TextFieldParser(resolvedCsvPath)
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = parser.ReadFields() ?? [];
        var headerIndexes = headers
            .Select((header, index) => new { Header = header, Index = index })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields == null)
            {
                continue;
            }

            var title = GetField(fields, headerIndexes, "primaryTitle");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            yield return new CsvBookRow(
                title.Trim(),
                TrimToNull(GetField(fields, headerIndexes, "author") ?? GetField(fields, headerIndexes, "authorName")),
                TrimToNull(GetField(fields, headerIndexes, "contentType")),
                TrimToNull(GetField(fields, headerIndexes, "status")),
                TryDecimal(GetField(fields, headerIndexes, "currentChapterNumber")),
                TrimToNull(GetField(fields, headerIndexes, "currentChapterLabel")),
                TryDecimal(GetField(fields, headerIndexes, "totalChapters")),
                TryInt(GetField(fields, headerIndexes, "rating")),
                TryInt(GetField(fields, headerIndexes, "priority")),
                SplitSemicolonList(GetField(fields, headerIndexes, "genres")),
                SplitSemicolonList(GetField(fields, headerIndexes, "tags")),
                ParseAlternativeTitles(GetField(fields, headerIndexes, "alternativeTitles")),
                TrimToNull(GetField(fields, headerIndexes, "description")),
                TrimToNull(GetField(fields, headerIndexes, "notes")),
                DeserializeJsonList<CsvBookLink>(GetField(fields, headerIndexes, "links")),
                DeserializeJsonList<CsvBookProgressHistory>(GetField(fields, headerIndexes, "progressHistory")));
        }
    }

    private static string FindCsvPath(string? explicitCsvPath = null, [CallerFilePath] string sourceFilePath = "")
    {
        if (!string.IsNullOrWhiteSpace(explicitCsvPath))
        {
            if (!File.Exists(explicitCsvPath))
            {
                throw new FileNotFoundException($"The requested CSV test dataset was not found: {explicitCsvPath}",
                    explicitCsvPath);
            }

            return explicitCsvPath;
        }

        foreach (var startPath in new[]
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
                foreach (var relativePath in CsvRelativePaths)
                {
                    var candidate = Path.Combine(current.FullName, relativePath);
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
        return headerIndexes.TryGetValue(header, out var index) && index < fields.Count ? fields[index] : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal? TryDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static int? TryInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static IReadOnlyList<string> SplitSemicolonList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static IReadOnlyList<string> ParseAlternativeTitles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement
                .EnumerateArray()
                .Select(element => element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Object when element.TryGetProperty("title", out var title) => title.GetString(),
                    JsonValueKind.Object when element.TryGetProperty("Title", out var title) => title.GetString(),
                    _ => null
                })
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<T> DeserializeJsonList<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record CsvBookRow(
        string PrimaryTitle,
        string? Author,
        string? ContentType,
        string? Status,
        decimal? CurrentChapterNumber,
        string? CurrentChapterLabel,
        decimal? TotalChapters,
        int? Rating,
        int? Priority,
        IReadOnlyList<string> Genres,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> AlternativeTitles,
        string? Description,
        string? Notes,
        IReadOnlyList<CsvBookLink> Links,
        IReadOnlyList<CsvBookProgressHistory> ProgressHistory);

    private sealed record CsvBookLink(
        string Url,
        string? Label,
        string SourceType,
        bool IsPrimary,
        bool LastReadHere);

    private sealed record CsvBookProgressHistory(
        DateTimeOffset ChangedAt,
        decimal? ChapterNumber,
        string? ChapterLabel,
        string? Comment);

    private sealed record BookUniqueKey(Guid OwnerId, string NormalizedTitle, Guid ContentTypeId);
}

public enum BookCsvSeedProfile
{
    SyntheticBalanced,
    PreserveSource
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
    string? Author,
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
            book.Author?.PrimaryName,
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
