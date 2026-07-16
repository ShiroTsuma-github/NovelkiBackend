namespace Application.Common.Interfaces;

using Domain.Models;

public interface IBookSummaryQueryService
{
    public Task<BookSummarySnapshot> GetSummaryAsync(Guid ownerId, BookSearchCriteria criteria,
        CancellationToken cancellationToken);
}
