namespace Api.Controllers;

using Application.Common.Models;
using Application.Features.AccountFeatures.Commands;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v1/account")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IMediator mediator, ILogger<AccountController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting(DependencyInjection.AccountAuthRateLimitPolicy)]
    public async Task<IActionResult> Register(RegisterUserCommand registerUserCommand)
    {
        var response = await _mediator.Send(registerUserCommand);
        _logger.LogInformation("User registered. UserId={UserId} Username={Username}", response.Id,
            registerUserCommand.Username);
        return Ok(response);
    }

    [HttpPost("login")]
    [EnableRateLimiting(DependencyInjection.AccountAuthRateLimitPolicy)]
    public async Task<IActionResult> Login(LoginUserCommand loginUserCommand)
    {
        var response = await _mediator.Send(loginUserCommand);
        _logger.LogInformation(
            "User logged in. UserId={UserId} IdentifierType={IdentifierType}",
            response.UserId,
            string.IsNullOrWhiteSpace(loginUserCommand.Username) ? "Email" : "Username");
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand refreshTokenCommand)
    {
        var response = await _mediator.Send(refreshTokenCommand);
        _logger.LogInformation("Access token refreshed. UserId={UserId}", response.UserId);
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand logoutCommand)
    {
        await _mediator.Send(logoutCommand);
        return NoContent();
    }
}
