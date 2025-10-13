namespace Application.Features.StatusFeatures.Commands;

using Application.Common;
using Application.Common.DTOs.Status;

public record CreateStatusCommand(string Name, string? Description) : IRequest<StatusDto>;

public class CreateStatusCommandHandler : IRequestHandler<CreateStatusCommand, StatusDto>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<CreateStatusCommand, Status> _createMapper;
    private readonly IMapper<Status, StatusDto> _returnMapper;

    public CreateStatusCommandHandler(IStatusRepository statusRepository,
        IMapper<CreateStatusCommand, Status> createMapper,
        IMapper<Status, StatusDto> returnMapper)
    {
        _statusRepository = statusRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<StatusDto> Handle(CreateStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfFound(
            status,
            request.Name,
            g => g.Id);

        status = _createMapper.Map(request);
        await _statusRepository.AddAsync(status, cancellationToken);
        return _returnMapper.Map(status);
    }
}