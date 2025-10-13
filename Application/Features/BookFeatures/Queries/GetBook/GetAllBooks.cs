namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public record GetAllBooksQuery(int Skip = 0, int Take = 100) : IRequest<ICollection<BookDto>>;

public class GetBooksQueryHandler : IRequestHandler<GetAllBooksQuery, ICollection<BookDto>>
{
    private readonly IBookRepository _repository;
    public GetBooksQueryHandler(IBookRepository repository)
    {
        _repository = repository;
    }
    public async Task<ICollection<BookDto>> Handle(GetAllBooksQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
