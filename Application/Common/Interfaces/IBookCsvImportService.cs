namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookCsvImportService
{
    string CreateTemplate();
    Task<BookImportSessionDto> CreateSessionAsync(Stream csvStream, string fileName, CancellationToken cancellationToken);
    Task<BookImportSessionDto> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId, UpdateBookImportRowRequest request, CancellationToken cancellationToken);
    Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId, CancellationToken cancellationToken);
    Task<BookImportSessionDto> DeleteInvalidRowsAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<BookImportFinalizeResultDto> FinalizeAsync(Guid sessionId, CancellationToken cancellationToken);
    Task CancelAsync(Guid sessionId, CancellationToken cancellationToken);
}
