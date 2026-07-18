namespace Application.Features.TagFeatures.Commands;

using Common.DTOs.Tag;

public sealed record CreateTagCommand(string Name, string? Description = null) : IRequest<TagDto>;

public sealed class CreateTagCommandHandler(ITagRepository tagRepository, IUser user)
    : IRequestHandler<CreateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var name = MappingExtensions.CollapseWhitespace(request.Name);
        var existing = await tagRepository.GetByNameAsync(user.RequiredId, name, cancellationToken);
        if (existing is not null)
        {
            throw new EntityAlreadyExistsException<Tag, Guid>(name, existing.Id);
        }

        var tag = new Tag
        {
            OwnerId = user.RequiredId,
            Name = name,
            NormalizedName = MappingExtensions.NormalizeName(name),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        await tagRepository.AddAsync(tag, cancellationToken);
        return tag.ToDto();
    }
}
