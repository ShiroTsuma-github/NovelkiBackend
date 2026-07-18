namespace Application.Features.AuthorFeatures.Commands;

using Common.DTOs.Author;

public sealed record CreateAuthorCommand(string PrimaryName, IReadOnlyCollection<string>? OtherNames = null)
    : IRequest<AuthorDto>;

public sealed class CreateAuthorCommandHandler(IAuthorRepository authorRepository)
    : IRequestHandler<CreateAuthorCommand, AuthorDto>
{
    public async Task<AuthorDto> Handle(CreateAuthorCommand request, CancellationToken cancellationToken)
    {
        var primaryName = MappingExtensions.CollapseWhitespace(request.PrimaryName);
        var names = (request.OtherNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(MappingExtensions.CollapseWhitespace)
            .Append(primaryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var name in names)
        {
            var existing = await authorRepository.GetByNameAsync(name, cancellationToken);
            if (existing is not null)
            {
                throw new EntityAlreadyExistsException<Author, Guid>(name, existing.Id);
            }
        }

        var author = new Author
        {
            PrimaryName = primaryName,
            NormalizedPrimaryName = MappingExtensions.NormalizeName(primaryName)
        };
        author.Names.Add(new AuthorName
        {
            Name = primaryName,
            NormalizedName = author.NormalizedPrimaryName,
            IsPrimary = true,
            Source = "Manual"
        });
        foreach (var name in names.Where(name => MappingExtensions.NormalizeName(name) != author.NormalizedPrimaryName))
        {
            author.Names.Add(new AuthorName
            {
                Name = name,
                NormalizedName = MappingExtensions.NormalizeName(name),
                IsPrimary = false,
                Source = "Manual"
            });
        }

        await authorRepository.AddAsync(author, cancellationToken);
        return author.ToDto();
    }
}
