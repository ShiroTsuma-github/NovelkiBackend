namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;
using Domain.Repositories;

public interface IBookSearchSuggestionQueryService
{
    Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetSuggestionsAsync(
        Guid ownerId,
        string field,
        string? search,
        BookSearchCriteria? criteria,
        int take,
        CancellationToken cancellationToken);
}
