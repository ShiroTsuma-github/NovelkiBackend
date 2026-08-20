namespace Application.Features.BookFeatures.Queries;

public sealed record ResolvePendingCoverUploadQuery(Guid Token) : IRequest<Guid?>;

public sealed class ResolvePendingCoverUploadHandler(
    IBookCoverRepository coverRepository,
    IUser user) : IRequestHandler<ResolvePendingCoverUploadQuery, Guid?>
{
    public async Task<Guid?> Handle(ResolvePendingCoverUploadQuery request, CancellationToken cancellationToken)
    {
        var cover = await coverRepository.GetByPendingUploadTokenAsync(
            request.Token,
            user.RequiredId,
            cancellationToken);
        return cover?.BookId;
    }
}
