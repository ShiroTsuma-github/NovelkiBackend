namespace Application.Features.GenreFeatures.Commands;

using Application.Common;
using Application.Common.DTOs.Genre;

public record CreateGenreCommand(string Name, string? Description) : IRequest<GenreDto>;

public class CreateGenreCommandHandler : IRequestHandler<CreateGenreCommand, GenreDto>
{
    private readonly IGenreRepository _genreRepository;

    public CreateGenreCommandHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDto> Handle(CreateGenreCommand request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByNameAsync(request.Name, cancellationToken);
        if (genre != null)
        {
            throw new EntityAlreadyExistsException<Genre, Guid>(request.Name, genre.Id);
        }

        genre = request.ToEntity();
        await _genreRepository.AddAsync(genre, cancellationToken);
        return genre.ToDto();
    }
}
