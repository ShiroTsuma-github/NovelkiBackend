namespace Application.Features.TypeFeatures.Commands;

using Application.Common.DTOs.Type;
using Type = Domain.Entities.Type;

public sealed record UpdateTypeCommand : IRequest<TypeDto>
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateTypeCommandHandler : IRequestHandler<UpdateTypeCommand, TypeDto>
{
    private readonly ITypeRepository _typeRepository;
    private readonly IMapper<UpdateTypeCommand, Type> _createMapper;
    private readonly IMapper<Type, TypeDto> _returnMapper;

    public UpdateTypeCommandHandler(ITypeRepository genreRepository,
        IMapper<UpdateTypeCommand, Type> createMapper,
        IMapper<Type, TypeDto> returnMapper)
    {
        _typeRepository = genreRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<TypeDto> Handle(UpdateTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound(type, request.Id);

        _createMapper.Map(request, type);

        await _typeRepository.SaveAsync(cancellationToken);

        return _returnMapper.Map(type);
    }
}