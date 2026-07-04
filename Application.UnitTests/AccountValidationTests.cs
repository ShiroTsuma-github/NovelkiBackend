using Application.Features.AccountFeatures.Commands;
using Application.Features.AccountFeatures.Validators;

namespace Application.UnitTests;

public class AccountValidationTests
{
    [Fact]
    public void LoginValidator_ShouldRejectRequestWithoutUsernameOrEmail()
    {
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand { Password = "Password1!" };

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Either Username or Email"));
    }

    [Fact]
    public void RegisterValidator_ShouldAcceptValidPasswordPolicy()
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand
        {
            Username = "reader",
            Email = "reader@example.com",
            Password = "Password1!"
        };

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
