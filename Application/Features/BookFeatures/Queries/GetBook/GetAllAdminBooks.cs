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
    private readonly IBookRepository _bookRepository;
    private readonly IBookListReadRepository _repository;

    public GetAllAdminBooksHandler(IBookRepository bookRepository, IBookListReadRepository repository)
    {
        _bookRepository = bookRepository;
        _repository = repository;
    }

    public async Task<PaginatedResult<AdminBookListItemDto>> Handle(GetAllAdminBooksQuery request, CancellationToken cancellationToken)
    {
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var books = criteria.HasFilters
            ? await _repository.SearchAdminListAsync(criteria, request.Skip, request.Take, request.SortBy, request.SortDirection, cancellationToken)
            : await _repository.GetAllAdminListAsync(request.Skip, request.Take, request.SortBy, request.SortDirection, cancellationToken);
        var total = criteria.HasFilters
            ? await _bookRepository.GetSearchCountAsync(criteria, cancellationToken)
            : await _bookRepository.GetCountAsync(cancellationToken);

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
