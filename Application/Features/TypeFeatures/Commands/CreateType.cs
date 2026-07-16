namespace Application.Features.TypeFeatures.Commands;

using Common;
using Application.Common.DTOs.Type;

public record CreateTypeCommand(string Name, string? Description) : IRequest<TypeDto>;

public class CreateTypeCommandHandler : IRequestHandler<CreateTypeCommand, TypeDto>
{
    private readonly ITypeRepository _typeRepository;

    public CreateTypeCommandHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<TypeDto> Handle(CreateTypeCommand request, CancellationToken cancellationToken)
    {
        ContentType? type = await _typeRepository.GetByNameAsync(request.Name, cancellationToken);
        if (type != null)
        {
            throw new EntityAlreadyExistsException<ContentType, Guid>(request.Name, type.Id);
        }

        type = request.ToEntity();
        await _typeRepository.AddAsync(type, cancellationToken);
        return type.ToDto();
    }
}
