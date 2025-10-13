namespace Application.Features.StatusFeatures.Queries.GetStatus;

public record GetStatusQuery<TDto> : IRequest<TDto?>
{
    public Guid Id { get; }
    public GetStatusQuery(Guid id) => Id = id;
};

public class GetStatusQueryHandler<TDto> : IRequestHandler<GetStatusQuery<TDto>, TDto?>
{
    private readonly IStatusRepository _statusRepository;
    private readonly IMapper<Status, TDto> _statusMapper;

    public GetStatusQueryHandler(IStatusRepository statusRepository, IMapper<Status, TDto> statusMapper)
    {
        _statusRepository = statusRepository;
        _statusMapper = statusMapper;
    }

    public async Task<TDto?> Handle(GetStatusQuery<TDto> request, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.ThrowIfNotFound<Status, Guid>(status, request.Id);
        return _statusMapper.Map(status);
    }
}