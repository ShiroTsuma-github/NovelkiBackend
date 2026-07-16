namespace Application.Features.AccountFeatures.Commands;

using Common.DTOs.User;

public sealed record LoginUserCommand : IRequest<TokenResponse>
{
    public string? Username { get; set; }
    public required string Password { get; set; }
    public string? Email { get; set; }
}

public class LoginUserHandler : IRequestHandler<LoginUserCommand, TokenResponse>
{
    private readonly IIdentityService _identityService;

    public LoginUserHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<TokenResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        TokenResponse token =
            await _identityService.LoginUser(new LoginDto(request.Username, request.Email, request.Password),
                cancellationToken);
        return token;
    }
}
