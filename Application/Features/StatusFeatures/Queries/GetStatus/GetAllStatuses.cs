namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetAllStatusesQuery(int Skip = 0, int Take = 100) : IRequest<PaginatedResult<StatusDto>>;

public class GetAllStatusesQueryHandler : IRequestHandler<GetAllStatusesQuery, PaginatedResult<StatusDto>>
{
    private readonly IStatusRepository _statusRepository;

    public GetAllStatusesQueryHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<PaginatedResult<StatusDto>> Handle(GetAllStatusesQuery request, CancellationToken cancellationToken)
    {
        var statuses = await _statusRepository.GetAllAsync(request.Skip, request.Take, cancellationToken);
        var total = await _statusRepository.GetCountAsync(cancellationToken);
        return PaginatedResult<StatusDto>.Create(request.Skip, request.Take, total, statuses.Select(s => s.ToDto()));
    }
}

