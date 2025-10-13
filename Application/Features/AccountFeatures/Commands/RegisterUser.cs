namespace Application.Features.AccountFeatures.Commands;

using Application.Common.DTOs.User;

public sealed record RegisterUserCommand : IRequest<RegisterResponse>
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
}

public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, RegisterResponse>
{
    private readonly IIdentityService _identityService;

    public RegisterUserHandler(IIdentityService identityService) => _identityService = identityService;

    public async Task<RegisterResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.RegisterUser(new RegisterDto(request.Username, request.Email, request.Password), cancellationToken);
    }
}