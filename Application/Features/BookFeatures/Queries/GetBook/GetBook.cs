namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common.DTOs.Book;

public record GetBookQuery(Guid Id) : IRequest<BookDto?>;

public class GetBookHandler : IRequestHandler<GetBookQuery, BookDto?>
{
    private readonly IBookRepository _repository;

    public GetBookHandler(IBookRepository repository) => _repository = repository;

    public async Task<BookDto?> Handle(GetBookQuery request, CancellationToken cancellationToken)
    {
        var book = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (book == null) return null;

        return new BookDto { Id=book.Id, Title=book.Title, Author=book.Author.Name };
    }
}