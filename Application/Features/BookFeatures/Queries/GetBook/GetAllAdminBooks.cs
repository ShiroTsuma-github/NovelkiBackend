namespace Application.Features.BookFeatures.Queries.GetBook;

using Common.Interfaces;
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

    public async Task<PaginatedResult<AdminBookListItemDto>> Handle(GetAllAdminBooksQuery request,
        CancellationToken cancellationToken)
    {
        BookSearchCriteria criteria = BookSearchQueryParser.Parse(request.Query);
        IReadOnlyCollection<AdminBookListItemDto> books = await _queryService.GetAdminBooksAsync(criteria, request.Skip,
            request.Take, request.SortBy, request.SortDirection, cancellationToken);
        int total = await _queryService.GetAdminBookCountAsync(criteria, cancellationToken);

        return PaginatedResult<AdminBookListItemDto>.Create(request.Skip, request.Take, total, books);
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
        Book book = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new EntityNotFoundException<Book, Guid>(request.Id);

        return book.ToAdminDto();
    }
}
