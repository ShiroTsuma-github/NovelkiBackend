namespace Application.Features.StatusFeatures.Commands;
public record DeleteStatusCommand(Guid Id) : IRequest;

public class DeleteStatusCommandHandler : IRequestHandler<DeleteStatusCommand>
{
    private readonly IStatusRepository _statusRepository;

    public DeleteStatusCommandHandler(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public async Task Handle(DeleteStatusCommand request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound(status, request.Id);
        await _statusRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
