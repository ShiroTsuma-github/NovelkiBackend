namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;
using Type = Domain.Entities.Type;

public record GetAllTypesQuery(int Skip = 0, int Take = 100) : IRequest<PaginatedResult<TypeDto>>;

public class GetAllTypesQueryHandler : IRequestHandler<GetAllTypesQuery, PaginatedResult<TypeDto>>
{
    private readonly ITypeRepository _typeRepository;
    private readonly IMapper<Type, TypeDto> _typeMapper;

    public GetAllTypesQueryHandler(ITypeRepository typeRepository, IMapper<Type, TypeDto> typeMapper)
    {
        _typeRepository = typeRepository;
        _typeMapper = typeMapper;
    }

    public async Task<PaginatedResult<TypeDto>> Handle(GetAllTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _typeRepository.GetAllAsync(request.Skip, request.Take, cancellationToken);
        var total = await _typeRepository.GetCountAsync(cancellationToken);
        return new PaginatedResult<TypeDto>
        {
            Take = request.Take,
            Skip = request.Skip,
            Data = _typeMapper.Map(types).ToList(),
            Total = total
        };
    }
}

