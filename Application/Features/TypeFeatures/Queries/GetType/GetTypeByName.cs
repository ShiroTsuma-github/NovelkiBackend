namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;

public record GetTypeByNameQuery(string Name) : IRequest<TypeDto>;

public class GetTypeByNameQueryHandler : IRequestHandler<GetTypeByNameQuery, TypeDto>
{
    private readonly ITypeRepository _typeRepository;

    public GetTypeByNameQueryHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<TypeDto> Handle(GetTypeByNameQuery request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByNameAsync(request.Name, cancellationToken)
            ?? throw new EntityNotFoundException<ContentType, string>(request.Name);

        return type.ToDto();
    }
}
