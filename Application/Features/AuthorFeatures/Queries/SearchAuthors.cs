namespace Application.Features.AuthorFeatures.Queries;

using Application.Common.DTOs.Author;

public record SearchAuthorsQuery(string? Search = null, int Take = 10) : IRequest<IReadOnlyCollection<AuthorDto>>;

public class SearchAuthorsQueryHandler : IRequestHandler<SearchAuthorsQuery, IReadOnlyCollection<AuthorDto>>
{
    private readonly IAuthorRepository _authorRepository;

    public SearchAuthorsQueryHandler(IAuthorRepository authorRepository)
    {
        _authorRepository = authorRepository;
    }

    public async Task<IReadOnlyCollection<AuthorDto>> Handle(SearchAuthorsQuery request,
        CancellationToken cancellationToken)
    {
        IEnumerable<Author> authors =
            await _authorRepository.SearchAsync(request.Search, Math.Clamp(request.Take, 1, 50), cancellationToken);
        return authors.Select(a => a.ToDto()).ToList();
    }
}
