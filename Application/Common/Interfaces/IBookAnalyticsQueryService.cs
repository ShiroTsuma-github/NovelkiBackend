namespace Application.Common.Interfaces;

using Domain.Models;

public interface IBookAnalyticsQueryService
{
    Task<BookAnalyticsSnapshot> GetAnalyticsAsync(
        Guid ownerId,
        BookSearchCriteria criteria,
        BookAnalyticsScopeSnapshot scope,
        CancellationToken cancellationToken);
}
