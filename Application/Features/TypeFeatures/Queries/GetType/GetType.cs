namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;

public record GetTypeQuery(Guid Id) : IRequest<TypeDto>;

public class GetTypeQueryHandler : IRequestHandler<GetTypeQuery, TypeDto>
{
    private readonly ITypeRepository _typeRepository;

    public GetTypeQueryHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<TypeDto> Handle(GetTypeQuery request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException<ContentType, Guid>(request.Id);

        return type.ToDto();
    }
}
