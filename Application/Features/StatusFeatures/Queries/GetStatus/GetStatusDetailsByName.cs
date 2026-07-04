namespace Application.Features.StatusFeatures.Queries.GetStatus;

using Application.Common.DTOs.Status;

public record GetStatusDetailsByNameQuery(string Name) : IRequest<StatusDetailsDto>;

public class GetStatusDetailsByNameQueryHandler : IRequestHandler<GetStatusDetailsByNameQuery, StatusDetailsDto>
{
    private readonly IStatusRepository _statusRepository;

    public GetStatusDetailsByNameQueryHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<StatusDetailsDto> Handle(GetStatusDetailsByNameQuery request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByNameAsync(request.Name, cancellationToken)
            ?? throw new EntityNotFoundException<Status, string>(request.Name);

        return status.ToDetailsDto();
    }
}
