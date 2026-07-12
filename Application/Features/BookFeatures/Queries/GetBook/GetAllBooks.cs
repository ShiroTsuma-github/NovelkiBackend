namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public record GetAllBooksQuery(
    int Skip = 0,
    int Take = 100,
    string? Query = null,
    string? SortBy = "lastModified",
    string? SortDirection = "desc",
    bool AdvanceCycle = false) : IRequest<PaginatedResult<BookDto>>;

public class GetBooksQueryHandler : IRequestHandler<GetAllBooksQuery, PaginatedResult<BookDto>>
{
    private readonly IBookRepository _repository;
    private readonly IBookListCache _cache;
    private readonly IUser _user;

    public GetBooksQueryHandler(IBookRepository repository, IBookListCache cache, IUser user)
    {
        _repository = repository;
        _cache = cache;
        _user = user;
    }

    public async Task<PaginatedResult<BookDto>> Handle(GetAllBooksQuery request, CancellationToken cancellationToken)
    {
        var ownerId = _user.RequiredId;
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var effectiveSortDirection = request.AdvanceCycle && IsCyclicSort(request.SortBy)
            ? await _repository.GetNextCycleSortDirectionAsync(ownerId, criteria, request.SortBy!, request.SortDirection, cancellationToken)
            : request.SortDirection;
        var cached = await _cache.GetBooksAsync(ownerId, request.Skip, request.Take, request.Query, request.SortBy, effectiveSortDirection, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var books = criteria.HasFilters
            ? await _repository.SearchAsync(ownerId, criteria, request.Skip, request.Take, request.SortBy, effectiveSortDirection, cancellationToken)
            : await _repository.GetAllAsync(ownerId, request.Skip, request.Take, request.SortBy, effectiveSortDirection, cancellationToken);
        var total = criteria.HasFilters
            ? await _repository.GetSearchCountAsync(ownerId, criteria, cancellationToken)
            : await _repository.GetCountAsync(ownerId, cancellationToken);
        var result = new PaginatedResult<BookDto>
        {
            Skip = request.Skip,
            Take = request.Take,
            Total = total,
            Data = books.Select(b => b.ToDto()).ToList()
        };
        await _cache.SetBooksAsync(ownerId, request.Skip, request.Take, request.Query, request.SortBy, effectiveSortDirection, result, cancellationToken);
        return result;
    }

    private static bool IsCyclicSort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() is "status" or "type" or "contenttype";
    }
}
