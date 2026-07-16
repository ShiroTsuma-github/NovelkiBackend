namespace Application.Features.BookFeatures.Queries.GetBook;

using Common.Interfaces;
using Application.Common.DTOs.Book;

public record GetAllBooksQuery(
    int Skip = 0,
    int Take = 100,
    string? Query = null,
    string? SortBy = "lastModified",
    string? SortDirection = "desc",
    bool AdvanceCycle = false) : IRequest<PaginatedResult<BookListItemDto>>;

public class GetBooksQueryHandler : IRequestHandler<GetAllBooksQuery, PaginatedResult<BookListItemDto>>
{
    private readonly IBookListQueryService _queryService;
    private readonly IBookListCache _cache;
    private readonly IUser _user;

    public GetBooksQueryHandler(IBookListQueryService queryService, IBookListCache cache, IUser user)
    {
        _queryService = queryService;
        _cache = cache;
        _user = user;
    }

    public async Task<PaginatedResult<BookListItemDto>> Handle(GetAllBooksQuery request,
        CancellationToken cancellationToken)
    {
        Guid ownerId = _user.RequiredId;
        BookSearchCriteria criteria = BookSearchQueryParser.Parse(request.Query);
        string? effectiveSortDirection = request.AdvanceCycle && IsCyclicSort(request.SortBy)
            ? await _queryService.GetNextCycleSortDirectionAsync(ownerId, criteria, request.SortBy!,
                request.SortDirection, cancellationToken)
            : request.SortDirection;
        PaginatedResult<BookListItemDto>? cached = await _cache.GetBooksAsync(ownerId, request.Skip, request.Take,
            request.Query, request.SortBy, effectiveSortDirection, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        IReadOnlyCollection<BookListItemDto> books = await _queryService.GetBooksAsync(ownerId, criteria, request.Skip,
            request.Take, request.SortBy, effectiveSortDirection, cancellationToken);
        int total = await _queryService.GetBookCountAsync(ownerId, criteria, cancellationToken);
        var result = PaginatedResult<BookListItemDto>.Create(request.Skip, request.Take, total, books);
        await _cache.SetBooksAsync(ownerId, request.Skip, request.Take, request.Query, request.SortBy,
            effectiveSortDirection, result, cancellationToken);
        return result;
    }

    private static bool IsCyclicSort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() is "status" or "type" or "contenttype";
    }
}

public record GetAllBooksForExportQuery(
    int Skip = 0,
    int Take = 100,
    string? Query = null,
    string? SortBy = "lastModified",
    string? SortDirection = "desc") : IRequest<PaginatedResult<BookDto>>;

public sealed class GetBooksForExportQueryHandler : IRequestHandler<GetAllBooksForExportQuery, PaginatedResult<BookDto>>
{
    private readonly IBookExportQueryService _queryService;
    private readonly IUser _user;

    public GetBooksForExportQueryHandler(IBookExportQueryService queryService, IUser user)
    {
        _queryService = queryService;
        _user = user;
    }

    public async Task<PaginatedResult<BookDto>> Handle(GetAllBooksForExportQuery request,
        CancellationToken cancellationToken)
    {
        Guid ownerId = _user.RequiredId;
        BookSearchCriteria criteria = BookSearchQueryParser.Parse(request.Query);
        return await _queryService.GetBooksForExportAsync(ownerId, criteria, request.Skip, request.Take, request.SortBy,
            request.SortDirection, cancellationToken);
    }
}
