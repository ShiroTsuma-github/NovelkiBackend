namespace Application.Features.StatusFeatures.Commands;

using Application.Common;
using Application.Common.DTOs.Status;

public record CreateStatusCommand(string Name, string? Description) : IRequest<StatusDto>;

public class CreateStatusCommandHandler : IRequestHandler<CreateStatusCommand, StatusDto>
{
    private readonly IStatusRepository _statusRepository;

    public CreateStatusCommandHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task<StatusDto> Handle(CreateStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByNameAsync(request.Name, cancellationToken);
        if (status != null)
        {
            throw new EntityAlreadyExistsException<Status, Guid>(request.Name, status.Id);
        }

        status = request.ToEntity();
        await _statusRepository.AddAsync(status, cancellationToken);
        return status.ToDto();
    }
}
