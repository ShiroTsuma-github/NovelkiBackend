namespace Application.Features.AccountFeatures;

using Common.DTOs.Admin;

public sealed record GetAdminUsersQuery(int Skip = 0, int Take = 50, string? Search = null)
    : IRequest<PaginatedResult<AdminUserDto>>;

public sealed class GetAdminUsersQueryHandler(IAdminAccountService service)
    : IRequestHandler<GetAdminUsersQuery, PaginatedResult<AdminUserDto>>
{
    public Task<PaginatedResult<AdminUserDto>> Handle(GetAdminUsersQuery request,
        CancellationToken cancellationToken)
    {
        return service.SearchAsync(request.Skip, request.Take, request.Search, cancellationToken);
    }
}

public sealed record DeleteAdminUserCommand(Guid UserId) : IRequest<AdminAccountDeleteResult>;

public sealed class DeleteAdminUserCommandHandler(IAdminAccountService service, IUser user)
    : IRequestHandler<DeleteAdminUserCommand, AdminAccountDeleteResult>
{
    public Task<AdminAccountDeleteResult> Handle(DeleteAdminUserCommand request,
        CancellationToken cancellationToken)
    {
        return service.DeleteAsync(request.UserId, user.RequiredId, cancellationToken);
    }
}
