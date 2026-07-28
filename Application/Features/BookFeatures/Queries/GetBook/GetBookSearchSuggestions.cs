namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public sealed record GetBookSearchSuggestionsQuery(
    string Field,
    string? Search = null,
    int Take = 10) : IRequest<IReadOnlyCollection<BookSearchSuggestionDto>>;

public sealed class GetBookSearchSuggestionsQueryHandler
    : IRequestHandler<GetBookSearchSuggestionsQuery, IReadOnlyCollection<BookSearchSuggestionDto>>
{
    private readonly IBookSearchSuggestionQueryService _queryService;
    private readonly IUser _user;

    public GetBookSearchSuggestionsQueryHandler(IBookSearchSuggestionQueryService queryService, IUser user)
    {
        _queryService = queryService;
        _user = user;
    }

    public Task<IReadOnlyCollection<BookSearchSuggestionDto>> Handle(
        GetBookSearchSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var field = request.Field?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(field) || !BookSearchSuggestionFields.All.Contains(field))
        {
            throw new ValidationException(
                $"Field must be one of: {string.Join(", ", BookSearchSuggestionFields.All.Order())}.");
        }

        return _queryService.GetSuggestionsAsync(
            _user.RequiredId,
            field,
            request.Search?.Trim(),
            Math.Clamp(request.Take, 1, 20),
            cancellationToken);
    }
}
