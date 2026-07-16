namespace Application.Features.TypeFeatures.Commands;

public record DeleteTypeCommand(Guid Id) : IRequest;

public class DeleteTypeCommandHandler : IRequestHandler<DeleteTypeCommand>
{
    private readonly ITypeRepository _typeRepository;

    public DeleteTypeCommandHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task Handle(DeleteTypeCommand request, CancellationToken cancellationToken)
    {
        _ = await _typeRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<ContentType, Guid>(request.Id);
        await _typeRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
