namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common;
using Application.Common.DTOs.Genre;

public record GetGenreQuery(Guid Id) : IRequest<GenreDto?>;

public class GetGenreQueryHandler : IRequestHandler<GetGenreQuery, GenreDto?>
{
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper<Genre, GenreDto> _genreMapper;

    public GetGenreQueryHandler(IGenreRepository genreRepository, IMapper<Genre, GenreDto> genreMapper)
    {
        _genreRepository = genreRepository;
        _genreMapper = genreMapper;
    }

    public async Task<GenreDto?> Handle(GetGenreQuery request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Genre, Guid>(genre, request.Id);
        return _genreMapper.Map(genre);
    }
}
