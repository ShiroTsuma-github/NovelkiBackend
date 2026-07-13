namespace Application.Common.Interfaces;

using Domain.Models;

public interface IBookSummaryQueryService
{
    Task<BookSummarySnapshot> GetSummaryAsync(Guid ownerId, BookSearchCriteria criteria, CancellationToken cancellationToken);
}
