namespace Application.Features.GenreFeatures.Queries.GetGenre;

public class GetGenreByNameQuery<TDto> : IRequest<TDto?>
{
    public string Name { get; }
    public GetGenreByNameQuery(string name) => Name = name;
}

public class GetGenreByNameQueryHandler<TDto> : IRequestHandler<GetGenreByNameQuery<TDto>, TDto?>
{
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper<Genre, TDto> _genreMapper;

    public GetGenreByNameQueryHandler(IGenreRepository genreRepository, IMapper<Genre, TDto> GenreMapper)
    {
        _genreRepository = genreRepository;
        _genreMapper = GenreMapper;
    }

    public async Task<TDto?> Handle(GetGenreByNameQuery<TDto> request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfNotFound<Genre, string>(genre, request.Name);
        return _genreMapper.Map(genre);
    }
}
