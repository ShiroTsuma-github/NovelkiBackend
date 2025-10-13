namespace Application.Features.BookFeatures.Commands;

public sealed record CreateBookCommand(
    string Title,
    string Author,
    string Type,
    string Status,
    IEnumerable<string> Genres,
    IEnumerable<string> Tags,
    int TotalChapters,
    int Chapter,
    int Grade,
    int Priority,
    string? Description,
    string? Notes,
    IEnumerable<string>? Links) : IRequest<Guid>;

public class CreateBookHandler : IRequestHandler<CreateBookCommand, Guid>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public CreateBookHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task<Guid> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        var book = new Book { Author = new Author { Name = request.Author}, Title = request.Title };
        await _repository.AddAsync(book, cancellationToken);

        return book.Id;
    }
}