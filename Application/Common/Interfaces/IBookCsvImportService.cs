namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookCsvImportService
{
    public string CreateTemplate();

    public Task<BookImportSessionDto> CreateSessionAsync(Stream csvStream, string fileName,
        CancellationToken cancellationToken);

    public Task<BookImportSessionDto> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    public Task<BookImportSessionDto> UpdateRowAsync(Guid sessionId, Guid rowId, UpdateBookImportRowRequest request,
        CancellationToken cancellationToken);

    public Task<BookImportSessionDto> DeleteRowAsync(Guid sessionId, Guid rowId, CancellationToken cancellationToken);
    public Task<BookImportSessionDto> DeleteInvalidRowsAsync(Guid sessionId, CancellationToken cancellationToken);
    public Task<BookImportFinalizeResultDto> FinalizeAsync(Guid sessionId, CancellationToken cancellationToken);
    public Task CancelAsync(Guid sessionId, CancellationToken cancellationToken);
}
