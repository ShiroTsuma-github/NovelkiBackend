namespace Application.Common.Interfaces;

public interface IAdminLibraryService
{
    public Task<AdminLibraryPurgeResult> DeleteAllBooksForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}

public sealed record AdminLibraryPurgeResult(int DeletedBooks, int DeletedAuthors, int DeletedTags);
