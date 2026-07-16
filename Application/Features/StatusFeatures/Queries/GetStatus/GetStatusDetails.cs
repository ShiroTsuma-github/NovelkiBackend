namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetStatusDetailsQuery(Guid Id) : IRequest<StatusDetailsDto>;

public class GetStatusDetailsQueryHandler : IRequestHandler<GetStatusDetailsQuery, StatusDetailsDto>
{
    private readonly IStatusRepository _statusRepository;

    public GetStatusDetailsQueryHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<StatusDetailsDto> Handle(GetStatusDetailsQuery request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new EntityNotFoundException<Status, Guid>(request.Id);

        return status.ToDetailsDto();
    }
}
