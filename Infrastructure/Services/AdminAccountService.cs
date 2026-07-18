namespace Infrastructure.Services;

using Application.Common.DTOs.Admin;
using Application.Common.Models;

public sealed class AdminAccountService(
    ApplicationDbContext context,
    IAdminLibraryService libraryService) : IAdminAccountService
{
    public async Task<PaginatedResult<AdminUserDto>> SearchAsync(int skip, int take, string? search,
        CancellationToken cancellationToken)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);
        var query = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToUpperInvariant();
            query = query.Where(user =>
                (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalized)) ||
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.UserName)
            .Skip(skip)
            .Take(take)
            .Select(user => new AdminUserDto(
                user.Id,
                user.UserName,
                user.Email,
                user.CreatedAt,
                user.Books.Count,
                user.OwnedTags.Count,
                context.Authors.Count(author => author.CreatedBy == user.Id)))
            .ToListAsync(cancellationToken);
        return PaginatedResult<AdminUserDto>.Create(skip, take, total, users);
    }

    public async Task<AdminAccountDeleteResult> DeleteAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken)
    {
        if (userId == currentAdminId)
        {
            throw new CannotDeleteCurrentAccountException();
        }

        if (!await context.Users.AsNoTracking().AnyAsync(item => item.Id == userId, cancellationToken))
        {
            throw new EntityNotFoundException<User, Guid>(userId);
        }

        var purge = await libraryService.DeleteAllBooksForOwnerAsync(userId, cancellationToken);
        context.ChangeTracker.Clear();

        var extraDeletedAuthors = await context.Authors
            .Where(author => author.CreatedBy == userId && !author.Books.Any())
            .ExecuteDeleteAsync(cancellationToken);
        await context.Authors
            .Where(author => author.CreatedBy == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(author => author.CreatedBy, (Guid?)null),
                cancellationToken);
        await context.Tags
            .Where(tag => tag.IsGlobal && tag.CreatedBy == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(tag => tag.CreatedBy, (Guid?)null), cancellationToken);
        var remainingTags = await context.Tags.CountAsync(tag => tag.OwnerId == userId, cancellationToken);

        var user = await context.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);

        return new AdminAccountDeleteResult(
            userId,
            purge.DeletedBooks,
            purge.DeletedAuthors + extraDeletedAuthors,
            purge.DeletedTags + remainingTags);
    }
}
