namespace Application.Features.BookFeatures.Commands;

using Common.Interfaces;

public sealed record DeleteBooksByOwnerCommand(Guid OwnerId) : IRequest<AdminLibraryPurgeResult>;

public sealed class DeleteBooksByOwnerHandler : IRequestHandler<DeleteBooksByOwnerCommand, AdminLibraryPurgeResult>
{
    private readonly IAdminLibraryService _adminLibraryService;

    public DeleteBooksByOwnerHandler(IAdminLibraryService adminLibraryService)
    {
        _adminLibraryService = adminLibraryService;
    }

    public Task<AdminLibraryPurgeResult> Handle(DeleteBooksByOwnerCommand request, CancellationToken cancellationToken)
    {
        return _adminLibraryService.DeleteAllBooksForOwnerAsync(request.OwnerId, cancellationToken);
    }
}
