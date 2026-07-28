namespace Application.Common.Interfaces;

using Application.Common.DTOs.Book;

public interface IBookSearchSuggestionQueryService
{
    Task<IReadOnlyCollection<BookSearchSuggestionDto>> GetSuggestionsAsync(
        Guid ownerId,
        string field,
        string? search,
        int take,
        CancellationToken cancellationToken);
}
