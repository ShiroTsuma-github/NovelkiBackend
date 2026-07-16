using Application.Features.AccountFeatures.Commands;
using Application.Features.AccountFeatures.Validators;

namespace Application.UnitTests;

using FluentValidation.Results;

public class AccountValidationTests
{
    [Fact]
    public void LoginValidator_ShouldRejectRequestWithoutUsernameOrEmail()
    {
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand { Password = "Password1!" };

        ValidationResult? result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Either Username or Email"));
    }

    [Fact]
    public void LoginValidator_ShouldAcceptUsernameOnly()
    {
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand { Username = "reader", Password = "Password1!" };

        ValidationResult? result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LoginValidator_ShouldAcceptEmailOnly()
    {
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand { Email = "reader@example.com", Password = "Password1!" };

        ValidationResult? result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LoginValidator_ShouldRejectUsernameAndEmailTogether()
    {
        var validator = new LoginUserCommandValidator();
        var command =
            new LoginUserCommand { Username = "reader", Email = "reader@example.com", Password = "Password1!" };

        ValidationResult? result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Either Username or Email"));
    }

    [Fact]
    public void RegisterValidator_ShouldAcceptValidPasswordPolicy()
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand
        {
            Username = "reader", Email = "reader@example.com", Password = "Password1!"
        };

        ValidationResult? result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("reader~name")]
    [InlineData("reader name")]
    [InlineData("reader/name")]
    [InlineData("reader:name")]
    public void RegisterValidator_ShouldRejectUsernameCharactersThatIdentityRejects(string username)
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand
        {
            Username = username, Email = "reader@example.com", Password = "Password1!"
        };

        ValidationResult? result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterUserCommand.Username));
    }

    [Theory]
    [InlineData("reader-name")]
    [InlineData("reader.name")]
    [InlineData("reader_name")]
    [InlineData("reader+name")]
    [InlineData("reader@name")]
    public void RegisterValidator_ShouldAcceptIdentityAllowedUsernameCharacters(string username)
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand
        {
            Username = username, Email = "reader@example.com", Password = "Password1!"
        };

        ValidationResult? result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
