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

    public UpdateStatusCommandHandler(IStatusRepository genreRepository)
    {
        _statusRepository = genreRepository;
    }

    public async Task<StatusDto> Handle(UpdateStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<Status, Guid>(request.Id);

        request.ApplyTo(status);

        await _statusRepository.SaveAsync(cancellationToken);

        return status.ToDto();
    }
}
