namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;

public record GetAllTypesQuery(int Skip = 0, int Take = 100) : IRequest<PaginatedResult<TypeDto>>;

public class GetAllTypesQueryHandler : IRequestHandler<GetAllTypesQuery, PaginatedResult<TypeDto>>
{
    private readonly ITypeRepository _typeRepository;

    public GetAllTypesQueryHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<PaginatedResult<TypeDto>> Handle(GetAllTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _typeRepository.GetAllAsync(request.Skip, request.Take, cancellationToken);
        var total = await _typeRepository.GetCountAsync(cancellationToken);
        return new PaginatedResult<TypeDto>
        {
            Take = request.Take,
            Skip = request.Skip,
            Data = types.Select(t => t.ToDto()).ToList(),
            Total = total
        };
    }
}

