namespace Application.Features.GenreFeatures.Commands;

using Application.Common;

public record DeleteGenreCommand(Guid Id) : IRequest;

public class DeleteGenreCommandHandler : IRequestHandler<DeleteGenreCommand>
{
    private readonly IGenreRepository _genreRepository;

    public DeleteGenreCommandHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task Handle(DeleteGenreCommand request, CancellationToken cancellationToken)
    {
        _ = await _genreRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<Genre, Guid>(request.Id);
        await _genreRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
