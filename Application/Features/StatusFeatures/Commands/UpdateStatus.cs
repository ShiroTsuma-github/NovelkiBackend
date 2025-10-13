namespace Application.Features.StatusFeatures.Commands;

using Application.Common.DTOs.Status;

public sealed record UpdateStatusCommand : IRequest<StatusDto>
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateStatusCommandHandler : IRequestHandler<UpdateStatusCommand, StatusDto>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<UpdateStatusCommand, Status> _createMapper;
    private readonly IMapper<Status, StatusDto> _returnMapper;

    public UpdateStatusCommandHandler(IStatusRepository genreRepository,
        IMapper<UpdateStatusCommand, Status> createMapper,
        IMapper<Status, StatusDto> returnMapper)
    {
        _statusRepository = genreRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<StatusDto> Handle(UpdateStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound(status, request.Id);

        _createMapper.Map(request, status);

        await _statusRepository.SaveAsync(cancellationToken);

        return _returnMapper.Map(status);
    }
}