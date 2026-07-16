namespace Infrastructure.Services;

using Application.Common;
using Application.Common.DTOs.Book;
using System.Globalization;

internal static class BookCsvImportRowMapper
{
    public static ImportRow CreateRow(IReadOnlyDictionary<string, string> values, int lineNumber)
    {
        return new ImportRow
        {
            RowId = Guid.NewGuid(),
            LineNumber = lineNumber,
            PrimaryTitle = CleanName(values, "primaryTitle"),
            AuthorName = CleanName(values, "authorName"),
            ContentType = CleanName(values, "contentType"),
            Status = CleanName(values, "status"),
            Tags = NormalizeTags(Clean(values, "tags")),
            TotalChapters = Clean(values, "totalChapters"),
            CurrentChapterNumber = Clean(values, "currentChapterNumber"),
            CurrentChapterLabel = Clean(values, "currentChapterLabel"),
            Rating = Clean(values, "rating"),
            Priority = Clean(values, "priority"),
            Description = Clean(values, "description"),
            Notes = NormalizeNotes(Clean(values, "notes")),
            RawImportedLine = Clean(values, "rawImportedLine")
        };
    }

    public static void ApplyRequest(ImportRow row, UpdateBookImportRowRequest request)
    {
        row.PrimaryTitle = request.PrimaryTitle;
        row.AuthorName = request.AuthorName;
        row.ContentType = request.ContentType;
        row.Status = request.Status;
        row.Tags = request.Tags;
        row.TotalChapters = request.TotalChapters;
        row.CurrentChapterNumber = request.CurrentChapterNumber;
        row.CurrentChapterLabel = request.CurrentChapterLabel;
        row.Rating = request.Rating;
        row.Priority = request.Priority;
        row.Description = request.Description;
        row.Notes = request.Notes;
        row.RawImportedLine = request.RawImportedLine;
        NormalizeRow(row);
    }

    public static void NormalizeRow(ImportRow row)
    {
        row.PrimaryTitle = NormalizeNameToNull(row.PrimaryTitle);
        row.AuthorName = NormalizeNameToNull(row.AuthorName);
        row.ContentType = NormalizeNameToNull(row.ContentType);
        row.Status = NormalizeNameToNull(row.Status);
        row.Tags = NormalizeTags(row.Tags);
        row.TotalChapters = TrimToNull(row.TotalChapters);
        row.CurrentChapterNumber = TrimToNull(row.CurrentChapterNumber);
        row.CurrentChapterLabel = TrimToNull(row.CurrentChapterLabel);
        row.Rating = TrimToNull(row.Rating);
        row.Priority = TrimToNull(row.Priority);
        row.Description = TrimToNull(row.Description);
        row.Notes = NormalizeNotes(row.Notes);
        row.RawImportedLine = TrimToNull(row.RawImportedLine);
    }

    public static IEnumerable<string> SplitTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(MappingExtensions.CollapseWhitespace)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }

    public static decimal? ParseDecimal(ImportRow row, string? value, string fieldKey, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be a valid number.");
            return null;
        }

        return parsed;
    }

    public static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    public static int? ParseInt(ImportRow row, string? value, string fieldKey, string fieldName, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be a valid integer.");
            return null;
        }

        if (parsed < min || parsed > max)
        {
            AddFieldError(row, fieldKey, $"{fieldName} must be between {min} and {max}.");
        }

        return parsed;
    }

    public static void AddFieldError(ImportRow row, string fieldKey, string message)
    {
        row.Errors.Add(message);

        if (!row.FieldErrors.TryGetValue(fieldKey, out List<string>? errors))
        {
            errors = [];
            row.FieldErrors[fieldKey] = errors;
        }

        errors.Add(message);
    }

    private static string? Clean(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string? value) ? TrimToNull(value) : null;
    }

    private static string? CleanName(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string? value) ? NormalizeNameToNull(value) : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeNameToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : MappingExtensions.CollapseWhitespace(value);
    }

    private static string? NormalizeTags(string? value)
    {
        string[] tags = SplitTags(value).ToArray();
        return tags.Length == 0 ? null : string.Join("; ", tags);
    }

    private static string? NormalizeNotes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        IEnumerable<string> normalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split(['|', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        string result = string.Join('\n', normalized);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
