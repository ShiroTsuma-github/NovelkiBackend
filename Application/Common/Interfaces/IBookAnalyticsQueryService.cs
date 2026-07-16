namespace Application.Common.Interfaces;

using Domain.Models;

public interface IBookAnalyticsQueryService
{
    public Task<BookAnalyticsSnapshot> GetAnalyticsAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        BookAnalyticsScopeSnapshot scope,
        CancellationToken cancellationToken);
}
