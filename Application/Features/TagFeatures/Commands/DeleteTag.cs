namespace Application.Features.TagFeatures.Commands;

public sealed record DeleteTagCommand(Guid Id) : IRequest;

public sealed class DeleteTagCommandHandler(ITagRepository tagRepository, IUser user)
    : IRequestHandler<DeleteTagCommand>
{
    public async Task Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tagRepository.GetByIdAsync(user.RequiredId, request.Id, cancellationToken)
                  ?? throw new EntityNotFoundException<Tag, Guid>(request.Id);

        if (tag.BookTags.Count > 0)
        {
            throw new EntityInUseException<Tag>(tag.Name);
        }

        await tagRepository.DeleteAsync(tag, cancellationToken);
    }
}
