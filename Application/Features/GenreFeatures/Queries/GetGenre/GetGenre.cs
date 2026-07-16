namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common.DTOs.Genre;

public record GetGenreQuery(Guid Id) : IRequest<GenreDto>;

public class GetGenreQueryHandler : IRequestHandler<GetGenreQuery, GenreDto>
{
    private readonly IGenreRepository _genreRepository;

    public GetGenreQueryHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDto> Handle(GetGenreQuery request, CancellationToken cancellationToken)
    {
        Genre genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken)
                      ?? throw new EntityNotFoundException<Genre, Guid>(request.Id);

        return genre.ToDto();
    }
}
