namespace Application.Features.AccountFeatures.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<TokenResponse>;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    private readonly IIdentityService _identityService;

    public RefreshTokenHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return _identityService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
    }
}

public sealed record LogoutCommand(string? RefreshToken) : IRequest;

public class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IIdentityService _identityService;

    public LogoutHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        return _identityService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
    }
}
