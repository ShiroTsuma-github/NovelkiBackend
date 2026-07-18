namespace Application.Features.AuthorFeatures.Commands;

public sealed record DeleteAuthorCommand(Guid Id) : IRequest;

public sealed class DeleteAuthorCommandHandler(IAuthorLifecycleService lifecycleService, IUser user)
    : IRequestHandler<DeleteAuthorCommand>
{
    public async Task Handle(DeleteAuthorCommand request, CancellationToken cancellationToken)
    {
        await lifecycleService.DeleteAsync(
            request.Id,
            user.RequiredId,
            user.Roles.Contains(AuthorizationRoles.Admin, StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }
}
