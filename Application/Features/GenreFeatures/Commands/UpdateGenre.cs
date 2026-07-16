namespace Application.Features.GenreFeatures.Commands;

using Application.Common.DTOs.Genre;

public sealed record UpdateGenreCommand : IRequest<GenreDto>
{
    [JsonIgnore] public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateGenreCommandHandler : IRequestHandler<UpdateGenreCommand, GenreDto>
{
    private readonly IGenreRepository _genreRepository;

    public UpdateGenreCommandHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<GenreDto> Handle(UpdateGenreCommand request, CancellationToken cancellationToken)
    {
        var genre = await _genreRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new EntityNotFoundException<Genre, Guid>(request.Id);

        request.ApplyTo(genre);
        await _genreRepository.SaveAsync(cancellationToken);

        return genre.ToDto();
    }
}
