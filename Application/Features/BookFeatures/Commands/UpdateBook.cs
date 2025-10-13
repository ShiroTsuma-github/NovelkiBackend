namespace Application.Features.BookFeatures.Commands;

public record UpdateBookCommand(Guid Id, string Title, string Author) : IRequest;

public class UpdateBookHandler : IRequestHandler<UpdateBookCommand>
{
    private readonly IBookRepository _repository;

    public UpdateBookHandler(IBookRepository repository) => _repository = repository;

    public async Task Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}