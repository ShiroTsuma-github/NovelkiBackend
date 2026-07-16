namespace Application.Features.TypeFeatures.Commands;

using Application.Common.DTOs.Type;

public sealed record UpdateTypeCommand : IRequest<TypeDto>
{
    [JsonIgnore] public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateTypeCommandHandler : IRequestHandler<UpdateTypeCommand, TypeDto>
{
    private readonly ITypeRepository _typeRepository;

    public UpdateTypeCommandHandler(ITypeRepository genreRepository)
    {
        _typeRepository = genreRepository;
    }

    public async Task<TypeDto> Handle(UpdateTypeCommand request, CancellationToken cancellationToken)
    {
        ContentType type = await _typeRepository.GetByIdAsync(request.Id, cancellationToken)
                           ?? throw new EntityNotFoundException<ContentType, Guid>(request.Id);

        request.ApplyTo(type);

        await _typeRepository.SaveAsync(cancellationToken);

        return type.ToDto();
    }
}
