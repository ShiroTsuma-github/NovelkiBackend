namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public record GetAllBooksQuery(
    int Skip = 0,
    int Take = 100,
    string? Query = null,
    string? SortBy = "lastModified",
    string? SortDirection = "desc") : IRequest<PaginatedResult<BookDto>>;

public class GetBooksQueryHandler : IRequestHandler<GetAllBooksQuery, PaginatedResult<BookDto>>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public GetBooksQueryHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task<PaginatedResult<BookDto>> Handle(GetAllBooksQuery request, CancellationToken cancellationToken)
    {
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var books = criteria.HasFilters
            ? await _repository.SearchAsync(_user.RequiredId, criteria, request.Skip, request.Take, request.SortBy, request.SortDirection, cancellationToken)
            : await _repository.GetAllAsync(_user.RequiredId, request.Skip, request.Take, request.SortBy, request.SortDirection, cancellationToken);
        var total = criteria.HasFilters
            ? await _repository.GetSearchCountAsync(_user.RequiredId, criteria, cancellationToken)
            : await _repository.GetCountAsync(_user.RequiredId, cancellationToken);
        return new PaginatedResult<BookDto>
        {
            Skip = request.Skip,
            Take = request.Take,
            Total = total,
            Data = books.Select(b => b.ToDto()).ToList()
        };
    }
}
