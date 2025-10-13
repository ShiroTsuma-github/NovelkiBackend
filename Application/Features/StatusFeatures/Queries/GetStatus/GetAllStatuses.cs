namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetAllStatusesQuery(int Skip = 0, int Take = 100) : IRequest<PaginatedResult<StatusDto>>;

public class GetAllStatusesQueryHandler : IRequestHandler<GetAllStatusesQuery, PaginatedResult<StatusDto>>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<Status, StatusDto> _statusMapper;

    public GetAllStatusesQueryHandler(IStatusRepository statusRepository, IMapper<Status, StatusDto> statusMapper)
    {
        _statusRepository = statusRepository;
        _statusMapper = statusMapper;
    }

    public async Task<PaginatedResult<StatusDto>> Handle(GetAllStatusesQuery request, CancellationToken cancellationToken)
    {
        var statuses = await _statusRepository.GetAllAsync(request.Skip, request.Take, cancellationToken);
        var total = await _statusRepository.GetCountAsync(cancellationToken);
        return new PaginatedResult<StatusDto>
        {
            Take = request.Take,
            Skip = request.Skip,
            Data = _statusMapper.Map(statuses).ToList(),
            Total = total
        };
    }
}

