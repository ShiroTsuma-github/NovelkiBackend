namespace Application.Features.TagFeatures.Commands;

using Common.DTOs.Tag;

public sealed record UpdateTagCommand : IRequest<TagDto>
{
    [JsonIgnore] public Guid Id { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdateTagCommandHandler(ITagRepository tagRepository, IUser user)
    : IRequestHandler<UpdateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tagRepository.GetByIdAsync(user.RequiredId, request.Id, cancellationToken)
                  ?? throw new EntityNotFoundException<Tag, Guid>(request.Id);

        tag.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await tagRepository.SaveAsync(cancellationToken);
        return tag.ToDto();
    }
}
