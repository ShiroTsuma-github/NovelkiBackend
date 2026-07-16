namespace Application.Features.GenreFeatures.Queries.GetGenre;

using Application.Common.DTOs.Genre;

public record GetAllGenresQuery(int Skip = 0, int Take = 100) : IRequest<PaginatedResult<GenreDto>>;

public class GetAllGenresQueryHandler : IRequestHandler<GetAllGenresQuery, PaginatedResult<GenreDto>>
{
    private readonly IGenreRepository _genreRepository;

    public GetAllGenresQueryHandler(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    public async Task<PaginatedResult<GenreDto>> Handle(GetAllGenresQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<Genre> genres = await _genreRepository.GetAllAsync(request.Skip, request.Take, cancellationToken);
        int total = await _genreRepository.GetCountAsync(cancellationToken);
        return PaginatedResult<GenreDto>.Create(request.Skip, request.Take, total, genres.Select(g => g.ToDto()));
    }
}
