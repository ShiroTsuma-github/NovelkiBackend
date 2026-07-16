namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetStatusQuery(Guid Id) : IRequest<StatusDto>;

public class GetStatusQueryHandler : IRequestHandler<GetStatusQuery, StatusDto>
{
    private readonly IStatusRepository _statusRepository;

    public GetStatusQueryHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<StatusDto> Handle(GetStatusQuery request, CancellationToken cancellationToken)
    {
        Status status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new EntityNotFoundException<Status, Guid>(request.Id);

        return status.ToDto();
    }
}
