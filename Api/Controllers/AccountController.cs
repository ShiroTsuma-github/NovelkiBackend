namespace Api.Controllers;

using Application.Features.AccountFeatures.Commands;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserCommand registerUserCommand)
    {
        var response = await _mediator.Send(registerUserCommand);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginUserCommand loginUserCommand)
    {
        var response = await _mediator.Send(loginUserCommand);
        return Ok(response);
    }
}
