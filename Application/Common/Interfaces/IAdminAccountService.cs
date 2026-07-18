namespace Application.Common.Interfaces;

using Application.Common.DTOs.Admin;

public interface IAdminAccountService
{
    Task<PaginatedResult<AdminUserDto>> SearchAsync(int skip, int take, string? search,
        CancellationToken cancellationToken);
    Task<AdminAccountDeleteResult> DeleteAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken);
}
