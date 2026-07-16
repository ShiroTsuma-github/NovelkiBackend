namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public record GetBookQuery(Guid Id) : IRequest<BookDto>;

public class GetBookHandler : IRequestHandler<GetBookQuery, BookDto>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public GetBookHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task<BookDto> Handle(GetBookQuery request, CancellationToken cancellationToken)
    {
        var book = await _repository.GetByIdAsync(request.Id, _user.RequiredId, cancellationToken)
                   ?? throw new EntityNotFoundException<Book, Guid>(request.Id);

        return book.ToDto();
    }
}
