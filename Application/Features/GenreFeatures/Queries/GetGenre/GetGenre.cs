namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common;
using Application.Common.DTOs.Genre;

public record GetGenreQuery<TDto> : IRequest<TDto?>
{
    public Guid Id { get; }
    public GetGenreQuery(Guid id) => Id = id;
};

public class GetGenreQueryHandler<TDto> : IRequestHandler<GetGenreQuery<TDto>, TDto?>
{
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper<Genre, TDto> _genreMapper;

    public GetGenreQueryHandler(IGenreRepository genreRepository, IMapper<Genre, TDto> genreMapper)
    {
        _genreRepository = genreRepository;
        _genreMapper = genreMapper;
    }

    public async Task<TDto?> Handle(GetGenreQuery<TDto> request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Genre, Guid>(genre, request.Id);
        return _genreMapper.Map(genre);
    }
}
