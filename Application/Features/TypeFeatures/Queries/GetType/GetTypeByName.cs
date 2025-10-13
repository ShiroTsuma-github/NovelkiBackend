namespace Application.Features.TypeFeatures.Queries.GetType;

using Type = Domain.Entities.Type;

public class GetTypeByNameQuery<TDto> : IRequest<TDto?>
{
    public string Name { get; }
    public GetTypeByNameQuery(string name) => Name = name;
}

public class GetTypeByNameQueryHandler<TDto> : IRequestHandler<GetTypeByNameQuery<TDto>, TDto?>
{
    private readonly ITypeRepository _typeRepository;
    private readonly IMapper<Type, TDto> _typeMapper;

    public GetTypeByNameQueryHandler(ITypeRepository typeRepository, IMapper<Type, TDto> typeMapper)
    {
        _typeRepository = typeRepository;
        _typeMapper = typeMapper;
    }

    public async Task<TDto?> Handle(GetTypeByNameQuery<TDto> request, CancellationToken cancellationToken)
    {
        var type = await _typeRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfNotFound<Type, string>(type, request.Name);
        return _typeMapper.Map(type);
    }
}

