namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetStatusByNameQuery(string Name) : IRequest<StatusDto>;

public class GetStatusByNameQueryHandler : IRequestHandler<GetStatusByNameQuery, StatusDto>
{
    private readonly IStatusRepository _statusRepository;

    public GetStatusByNameQueryHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<StatusDto> Handle(GetStatusByNameQuery request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByNameAsync(request.Name, cancellationToken)
            ?? throw new EntityNotFoundException<Status, string>(request.Name);

        return status.ToDto();
    }
}
