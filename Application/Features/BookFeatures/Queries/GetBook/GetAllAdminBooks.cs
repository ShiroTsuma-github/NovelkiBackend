namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.Interfaces;
using Application.Common.DTOs.Book;

public record GetAllAdminBooksQuery(
    int Skip = 0,
    int Take = 100,
    string? Query = null,
    string? SortBy = null,
    string? SortDirection = null) : IRequest<PaginatedResult<AdminBookListItemDto>>;

public class GetAllAdminBooksHandler : IRequestHandler<GetAllAdminBooksQuery, PaginatedResult<AdminBookListItemDto>>
{
    private readonly IBookListQueryService _queryService;

    public GetAllAdminBooksHandler(IBookListQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<PaginatedResult<AdminBookListItemDto>> Handle(GetAllAdminBooksQuery request, CancellationToken cancellationToken)
    {
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var books = await _queryService.GetAdminBooksAsync(criteria, request.Skip, request.Take, request.SortBy, request.SortDirection, cancellationToken);
        var total = await _queryService.GetAdminBookCountAsync(criteria, cancellationToken);

        return new PaginatedResult<AdminBookListItemDto>
        {
            Skip = request.Skip,
            Take = request.Take,
            Total = total,
            Data = books.ToList()
        };
    }
}

public record GetAdminBookQuery(Guid Id) : IRequest<AdminBookDto>;

public class GetAdminBookHandler : IRequestHandler<GetAdminBookQuery, AdminBookDto>
{
    private readonly IBookRepository _repository;

    public GetAdminBookHandler(IBookRepository repository)
    {
        _repository = repository;
    }

    public async Task<AdminBookDto> Handle(GetAdminBookQuery request, CancellationToken cancellationToken)
    {
        var book = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.Id);

        return book.ToAdminDto();
    }
}
