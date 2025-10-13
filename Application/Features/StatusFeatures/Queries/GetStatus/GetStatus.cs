namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetStatusQuery(Guid Id) : IRequest<StatusDto?>;

public class GetStatusQueryHandler : IRequestHandler<GetStatusQuery, StatusDto?>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<Status, StatusDto> _statusMapper;

    public GetStatusQueryHandler(IStatusRepository statusRepository, IMapper<Status, StatusDto> statusMapper)
    {
        _statusRepository = statusRepository;
        _statusMapper = statusMapper;
    }

    public async Task<StatusDto?> Handle(GetStatusQuery request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Status, Guid>(status, request.Id);
        return _statusMapper.Map(status);
    }
}
