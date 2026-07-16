namespace Application.Features.TypeFeatures.Queries.GetType;

using Application.Common.DTOs.Type;

public record GetTypeDetailsQuery(Guid Id) : IRequest<TypeDetailsDto>;

public class GetTypeDetailsQueryHandler : IRequestHandler<GetTypeDetailsQuery, TypeDetailsDto>
{
    private readonly ITypeRepository _typeRepository;

    public GetTypeDetailsQueryHandler(ITypeRepository typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<TypeDetailsDto> Handle(GetTypeDetailsQuery request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByIdAsync(request.Id, cancellationToken)
                   ?? throw new EntityNotFoundException<ContentType, Guid>(request.Id);

        return type.ToDetailsDto();
    }
}
