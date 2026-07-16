namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;

public record GetTypeDetailsByNameQuery(string Name) : IRequest<TypeDetailsDto>;

public class GetTypeDetailsByNameQueryHandler : IRequestHandler<GetTypeDetailsByNameQuery, TypeDetailsDto>
{
    private readonly ITypeRepository _typeRepository;

    public GetTypeDetailsByNameQueryHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<TypeDetailsDto> Handle(GetTypeDetailsByNameQuery request, CancellationToken cancellationToken)
    {
        ContentType type = await _typeRepository.GetByNameAsync(request.Name, cancellationToken)
                           ?? throw new EntityNotFoundException<ContentType, string>(request.Name);

        return type.ToDetailsDto();
    }
}
