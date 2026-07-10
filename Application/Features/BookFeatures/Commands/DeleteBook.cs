namespace Application.Features.BookFeatures.Commands;

public record DeleteBookCommand(Guid Id) : IRequest;

public class DeleteBookHandler : IRequestHandler<DeleteBookCommand>
{
    private readonly IBookRepository _repository;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IUser _user;

    public DeleteBookHandler(IBookRepository repository, IBookListCacheInvalidator cacheInvalidator, IUser user)
    {
        _repository = repository;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public async Task Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        _ = await _repository.GetByIdAsync(request.Id, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.Id);
        await _repository.DeleteAsync(request.Id, _user.RequiredId, cancellationToken);
        await _cacheInvalidator.InvalidateBooksAsync(_user.RequiredId, cancellationToken);
    }
}
