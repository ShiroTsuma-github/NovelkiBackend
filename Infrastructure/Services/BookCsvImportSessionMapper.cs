namespace Infrastructure.Services;

using Application.Common.DTOs.Book;

internal static class BookCsvImportSessionMapper
{
    public static BookImportSessionDto ToDto(ImportSession session)
    {
        return new BookImportSessionDto
        {
            SessionId = session.SessionId,
            FileName = session.FileName,
            TotalRows = session.Rows.Count,
            ValidRows = session.Rows.Count(row => row.Errors.Count == 0),
            InvalidRows = session.Rows.Count(row => row.Errors.Count > 0),
            CanFinalize = session.Rows.Count > 0 && session.Rows.All(row => row.Errors.Count == 0),
            AvailableContentTypes = session.AvailableContentTypes,
            AvailableStatuses = session.AvailableStatuses,
            Rows = session.Rows
                .OrderBy(row => row.LineNumber)
                .Select(row => new BookImportRowDto
                {
                    RowId = row.RowId,
                    LineNumber = row.LineNumber,
                    IsValid = row.Errors.Count == 0,
                    PrimaryTitle = row.PrimaryTitle,
                    AuthorName = row.AuthorName,
                    ContentType = row.ContentType,
                    Status = row.Status,
                    Genres = row.Genres,
                    Tags = row.Tags,
                    TotalChapters = row.TotalChapters,
                    CurrentChapterNumber = row.CurrentChapterNumber,
                    CurrentChapterLabel = row.CurrentChapterLabel,
                    Rating = row.Rating,
                    Priority = row.Priority,
                    Description = row.Description,
                    Notes = row.Notes,
                    RawImportedLine = row.RawImportedLine,
                    Errors = row.Errors.ToArray(),
                    FieldErrors = row.FieldErrors.ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyCollection<string>)pair.Value.ToArray())
                })
                .ToList()
        };
    }
}
