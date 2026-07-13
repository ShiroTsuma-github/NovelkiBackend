namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;

public sealed record GetBookSummaryQuery(string? Query = null) : IRequest<BookSummaryDto>;

public sealed class GetBookSummaryHandler : IRequestHandler<GetBookSummaryQuery, BookSummaryDto>
{
    private readonly IBookSummaryQueryService _queryService;
    private readonly IUser _user;

    public GetBookSummaryHandler(IBookSummaryQueryService queryService, IUser user)
    {
        _queryService = queryService;
        _user = user;
    }

    public async Task<BookSummaryDto> Handle(GetBookSummaryQuery request, CancellationToken cancellationToken)
    {
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var summary = await _queryService.GetSummaryAsync(_user.RequiredId, criteria, cancellationToken);

        return summary.ToDto();
    }
}
