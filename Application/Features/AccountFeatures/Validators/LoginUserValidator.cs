namespace Application.Features.AccountFeatures.Validators;

using Commands;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(AccountValidationMessages.PasswordRequired)
            .MinimumLength(8).WithMessage(AccountValidationMessages.PasswordTooShort)
            .MaximumLength(128).WithMessage(AccountValidationMessages.PasswordTooLong);

        RuleFor(x => x)
            .Must(OnlyOneProvided)
            .WithMessage("Either Username or Email must be provided, but not both.");

        When(x => !string.IsNullOrWhiteSpace(x.Username), () =>
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required when provided.")
                .MinimumLength(3).WithMessage(AccountValidationMessages.UsernameTooShort)
                .MaximumLength(32).WithMessage(AccountValidationMessages.UsernameTooLong);
        });
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required when provided.")
                .EmailAddress().WithMessage(AccountValidationMessages.InvalidEmail)
                .MaximumLength(100).WithMessage("Email can't be longer than 100 characters.");
        });
    }

    private bool OnlyOneProvided(LoginUserCommand command)
    {
        var usernameProvided = !string.IsNullOrWhiteSpace(command.Username);
        var emailProvided = !string.IsNullOrWhiteSpace(command.Email);
        return usernameProvided ^ emailProvided;
    }
}
