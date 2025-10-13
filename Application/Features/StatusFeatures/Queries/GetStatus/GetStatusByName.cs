namespace Application.Features.StatusFeatures.Queries.GetStatus;


public class GetStatusByNameQuery<TDto> : IRequest<TDto?>
{
    public string Name { get; }
    public GetStatusByNameQuery(string name) => Name = name;
}

public class GetStatusByNameQueryHandler<TDto> : IRequestHandler<GetStatusByNameQuery<TDto>, TDto?>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<Status, TDto> _statusMapper;

    public GetStatusByNameQueryHandler(IStatusRepository statusRepository, IMapper<Status, TDto> statusMapper)
    {
        _statusRepository = statusRepository;
        _statusMapper = statusMapper;
    }

    public async Task<TDto?> Handle(GetStatusByNameQuery<TDto> request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByNameAsync(request.Name, cancellationToken);
        Guard.ThrowIfNotFound<Status, string>(status, request.Name);
        return _statusMapper.Map(status);
    }
}

