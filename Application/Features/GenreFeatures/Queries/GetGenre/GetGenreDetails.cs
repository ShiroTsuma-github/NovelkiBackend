namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common.DTOs.Genre;

public record GetGenreDetailsQuery(Guid Id) : IRequest<GenreDetailsDto>;

public class GetGenreDetailsQueryHandler : IRequestHandler<GetGenreDetailsQuery, GenreDetailsDto>
{
    private readonly IGenreRepository _genreRepository;

    public GetGenreDetailsQueryHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDetailsDto> Handle(GetGenreDetailsQuery request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<Genre, Guid>(request.Id);

        return genre.ToDetailsDto();
    }
}
