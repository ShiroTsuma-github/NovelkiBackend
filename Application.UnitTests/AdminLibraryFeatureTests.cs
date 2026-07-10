using Application.Common.Interfaces;
using Application.Features.BookFeatures.Commands;

namespace Application.UnitTests;

public class AdminLibraryFeatureTests
{
    [Fact]
    public async Task DeleteBooksByOwnerHandler_ShouldDelegateToAdminLibraryService()
    {
        var ownerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var expected = new AdminLibraryPurgeResult(12, 3, 7);
        var service = new FakeAdminLibraryService(expected);
        var handler = new DeleteBooksByOwnerHandler(service);

        var result = await handler.Handle(new DeleteBooksByOwnerCommand(ownerId), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(ownerId, service.LastOwnerId);
        Assert.Equal(1, service.CallCount);
    }

    private sealed class FakeAdminLibraryService : IAdminLibraryService
    {
        private readonly AdminLibraryPurgeResult _result;

        public FakeAdminLibraryService(AdminLibraryPurgeResult result)
        {
            _result = result;
        }

        public Guid? LastOwnerId { get; private set; }
        public int CallCount { get; private set; }

        public Task<AdminLibraryPurgeResult> DeleteAllBooksForOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            LastOwnerId = ownerId;
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
