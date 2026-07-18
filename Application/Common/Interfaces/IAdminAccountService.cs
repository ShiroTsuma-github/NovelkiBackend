namespace Application.Common.Interfaces;

using DTOs.Admin;

public interface IAdminAccountService
{
    public Task<PaginatedResult<AdminUserDto>> SearchAsync(int skip, int take, string? search,
        CancellationToken cancellationToken);

    public Task<AdminAccountDeleteResult> DeleteAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken);
}
