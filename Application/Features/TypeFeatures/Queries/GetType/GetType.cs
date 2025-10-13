namespace Application.Features.TypeFeatures.Queries.GetType;

using Type = Domain.Entities.Type;

public record GetTypeQuery<TDto> : IRequest<TDto?>
{
    public Guid Id { get; }
    public GetTypeQuery(Guid id) => Id = id;
};

public class GetTypeQueryHandler<TDto> : IRequestHandler<GetTypeQuery<TDto>, TDto?>
{
    private readonly ITypeRepository _typeRepository;
    private readonly IMapper<Type, TDto> _typeMapper;

    public GetTypeQueryHandler(ITypeRepository typeRepository, IMapper<Type, TDto> typeMapper)
    {
        _typeRepository = typeRepository;
        _typeMapper = typeMapper;
    }

    public async Task<TDto?> Handle(GetTypeQuery<TDto> request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Type, Guid>(type, request.Id);
        return _typeMapper.Map(type);
    }
}