namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common.DTOs.Genre;

public record GetGenreDetailsByNameQuery(string Name) : IRequest<GenreDetailsDto>;

public class GetGenreDetailsByNameQueryHandler : IRequestHandler<GetGenreDetailsByNameQuery, GenreDetailsDto>
{
    private readonly IGenreRepository _genreRepository;

    public GetGenreDetailsByNameQueryHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDetailsDto> Handle(GetGenreDetailsByNameQuery request, CancellationToken cancellationToken)
    {
        Genre genre = await _genreRepository.GetByNameAsync(request.Name, cancellationToken)
                      ?? throw new EntityNotFoundException<Genre, string>(request.Name);

        return genre.ToDetailsDto();
    }
}
