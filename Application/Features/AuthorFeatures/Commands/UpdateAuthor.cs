namespace Application.Features.AuthorFeatures.Commands;

using Common.DTOs.Author;

public sealed record UpdateAuthorCommand : IRequest<AuthorDto>
{
    [JsonIgnore] public Guid Id { get; set; }
    public IReadOnlyCollection<string> OtherNames { get; set; } = Array.Empty<string>();
}

public sealed class UpdateAuthorCommandHandler(IAuthorRepository authorRepository, IUser user)
    : IRequestHandler<UpdateAuthorCommand, AuthorDto>
{
    public async Task<AuthorDto> Handle(UpdateAuthorCommand request, CancellationToken cancellationToken)
    {
        var author = await authorRepository.GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new EntityNotFoundException<Author, Guid>(request.Id);
        if (author.OwnerId != user.RequiredId &&
            !user.Roles.Contains(AuthorizationRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            throw new EntityNotFoundException<Author, Guid>(request.Id);
        }

        var names = request.OtherNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => MappingExtensions.CollapseWhitespace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in names)
        {
            var existing = author.OwnerId.HasValue
                ? await authorRepository.GetByNameAsync(author.OwnerId.Value, name, cancellationToken)
                : await authorRepository.GetPublicByNameAsync(name, cancellationToken);
            if (existing is not null && existing.Id != author.Id)
            {
                throw new EntityAlreadyExistsException<Author, Guid>(name, existing.Id);
            }
        }

        var desiredNames = names
            .Where(name => MappingExtensions.NormalizeName(name) != author.NormalizedPrimaryName)
            .ToDictionary(MappingExtensions.NormalizeName, StringComparer.Ordinal);
        var aliases = author.Names.Where(name => !name.IsPrimary).ToList();
        foreach (var alias in aliases.Where(alias => !desiredNames.ContainsKey(alias.NormalizedName)))
        {
            author.Names.Remove(alias);
        }

        foreach (var (normalizedName, name) in desiredNames)
        {
            var existingAlias = aliases.FirstOrDefault(alias => alias.NormalizedName == normalizedName);
            if (existingAlias is not null)
            {
                existingAlias.Name = name;
                continue;
            }

            authorRepository.AddName(author, new AuthorName
            {
                Name = name, NormalizedName = normalizedName, IsPrimary = false, Source = "Manual"
            });
        }

        await authorRepository.SaveAsync(cancellationToken);
        return author.ToDto();
    }
}
