namespace Application.Features.TypeFeatures.Commands;

using Application.Common;
using Application.Common.DTOs.Type;
using Type = Domain.Entities.Type;

public record CreateTypeCommand(string Name, string? Description) : IRequest<TypeDto>;

public class CreateTypeCommandHandler : IRequestHandler<CreateTypeCommand, TypeDto>
{
    private readonly ITypeRepository _typeRepository;
    private readonly IMapper<CreateTypeCommand, Type> _createMapper;
    private readonly IMapper<Type, TypeDto> _returnMapper;

    public CreateTypeCommandHandler(ITypeRepository typeRepository,
        IMapper<CreateTypeCommand, Type> createMapper,
        IMapper<Type, TypeDto> returnMapper)
    {
        _typeRepository = typeRepository;
        _createMapper = createMapper;
        _returnMapper = returnMapper;
    }

    public async Task<TypeDto> Handle(CreateTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfFound(
            type,
            request.Name,
            g => g.Id);

        type = _createMapper.Map(request);
        await _typeRepository.AddAsync(type, cancellationToken);
        return _returnMapper.Map(type);
    }
}