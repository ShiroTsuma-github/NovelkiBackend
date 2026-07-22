namespace Application.Features.AuthorFeatures.Commands;

using Common.DTOs.Author;

public sealed record UpdateAuthorVisibilityCommand(bool IsPublic) : IRequest<AuthorDto>
{
    [JsonIgnore] public Guid Id { get; set; }
}

public sealed class UpdateAuthorVisibilityCommandHandler(IAuthorLifecycleService lifecycleService, IUser user)
    : IRequestHandler<UpdateAuthorVisibilityCommand, AuthorDto>
{
    public async Task<AuthorDto> Handle(UpdateAuthorVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        var author = await lifecycleService.SetVisibilityAsync(
            request.Id,
            user.RequiredId,
            user.Roles.Contains(AuthorizationRoles.Admin, StringComparer.OrdinalIgnoreCase),
            request.IsPublic,
            cancellationToken);
        return author.ToDto(user.RequiredId);
    }
}
