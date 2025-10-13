namespace Application.Features.GenreFeatures.Commands;

using Application.Common;
using Application.Common.DTOs.Genre;

public record CreateGenreCommand(string Name, string? Description) : IRequest<GenreDto>;

public class CreateGenreCommandHandler : IRequestHandler<CreateGenreCommand, GenreDto>
{
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper<CreateGenreCommand, Genre> _createMapper;
    private readonly IMapper<Genre, GenreDto> _returnMapper;

    public CreateGenreCommandHandler(IGenreRepository genreRepository,
        IMapper<CreateGenreCommand, Genre> createMapper,
        IMapper<Genre, GenreDto> returnMapper)
    {
        _genreRepository = genreRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<GenreDto> Handle(CreateGenreCommand request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfFound<Genre, Guid>(
            genre,
            request.Name,
            g => g.Id);

        genre = _createMapper.Map(request);
        await _genreRepository.AddAsync(genre, cancellationToken);
        return _returnMapper.Map(genre);
    }
}