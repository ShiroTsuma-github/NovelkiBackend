namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common.DTOs.Genre;

public record GetGenreByNameQuery(string Name) : IRequest<GenreDto>;

public class GetGenreByNameQueryHandler : IRequestHandler<GetGenreByNameQuery, GenreDto>
{
    private readonly IGenreRepository _genreRepository;

    public GetGenreByNameQueryHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDto> Handle(GetGenreByNameQuery request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByNameAsync(request.Name, cancellationToken)
                    ?? throw new EntityNotFoundException<Genre, string>(request.Name);

        return genre.ToDto();
    }
}
