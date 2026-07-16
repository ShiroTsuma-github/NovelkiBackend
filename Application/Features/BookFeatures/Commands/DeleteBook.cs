namespace Application.Features.BookFeatures.Commands;

public record DeleteBookCommand(Guid Id) : IRequest;

public class DeleteBookHandler : IRequestHandler<DeleteBookCommand>
{
    private readonly IBookRepository _repository;
    private readonly IBookCoverStorage _storage;
    private readonly IBookListCacheInvalidator _cacheInvalidator;
    private readonly IUser _user;

    public DeleteBookHandler(
        IBookRepository repository,
        IBookCoverStorage storage,
        IBookListCacheInvalidator cacheInvalidator,
        IUser user)
    {
        _repository = repository;
        _storage = storage;
        _cacheInvalidator = cacheInvalidator;
        _user = user;
    }

    public async Task Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        Book book = await _repository.GetByIdAsync(request.Id, _user.RequiredId, cancellationToken)
                    ?? throw new EntityNotFoundException<Book, Guid>(request.Id);
        string? storagePath = book.Cover?.StoragePath;
        await _repository.DeleteAsync(request.Id, _user.RequiredId, cancellationToken);
        await _storage.DeleteIfExistsAsync(storagePath, cancellationToken);
        await _cacheInvalidator.InvalidateBooksAsync(_user.RequiredId, cancellationToken);
    }
}
