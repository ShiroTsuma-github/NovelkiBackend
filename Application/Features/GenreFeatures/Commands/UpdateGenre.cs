namespace Application.Features.GenreFeatures.Commands;

using Application.Common.DTOs.Genre;

public sealed record UpdateGenreCommand : IRequest<GenreDto>
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateGenreCommandHandler : IRequestHandler<UpdateGenreCommand, GenreDto>
{
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper<UpdateGenreCommand, Genre> _createMapper;
    private readonly IMapper<Genre, GenreDto> _returnMapper;

    public UpdateGenreCommandHandler(IGenreRepository genreRepository,
        IMapper<UpdateGenreCommand, Genre> createMapper,
        IMapper<Genre, GenreDto> returnMapper)
    {
        _genreRepository = genreRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<GenreDto> Handle(UpdateGenreCommand request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Genre, Guid>(genre, request.Id);

        _createMapper.Map(request, genre);
        await _genreRepository.SaveAsync(cancellationToken);

        return _returnMapper.Map(genre);
    }
}