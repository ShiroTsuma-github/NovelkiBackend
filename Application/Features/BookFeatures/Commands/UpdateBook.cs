namespace Application.Features.BookFeatures.Commands;

public record UpdateBookProgressCommand(Guid Id, decimal? CurrentChapterNumber, string? CurrentChapterLabel, string? Comment) : IRequest;

public class UpdateBookProgressHandler : IRequestHandler<UpdateBookProgressCommand>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public UpdateBookProgressHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task Handle(UpdateBookProgressCommand request, CancellationToken cancellationToken)
    {
        var book = await _repository.GetByIdAsync(request.Id, _user.RequiredId, cancellationToken)
            ?? throw new EntityNotFoundException<Book, Guid>(request.Id);

        var changed = book.CurrentChapterNumber != request.CurrentChapterNumber ||
                      book.CurrentChapterLabel != request.CurrentChapterLabel;
        book.CurrentChapterNumber = request.CurrentChapterNumber;
        book.CurrentChapterLabel = request.CurrentChapterLabel;
        if (changed)
        {
            book.ProgressHistory.Add(new BookProgressHistory
            {
                ChapterNumber = request.CurrentChapterNumber,
                ChapterLabel = request.CurrentChapterLabel,
                Comment = request.Comment
            });
        }

        await _repository.SaveAsync(cancellationToken);
    }
}
