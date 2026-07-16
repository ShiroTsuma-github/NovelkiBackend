namespace Application.Features.AccountFeatures.Validators;

using Commands;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage(AccountValidationMessages.UsernameTooShort)
            .MaximumLength(32).WithMessage(AccountValidationMessages.UsernameTooLong)
            .Matches("^[A-Za-z0-9._@+-]+$")
            .WithMessage("Username can contain only letters, numbers, and -._@+ characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage(AccountValidationMessages.InvalidEmail)
            .MaximumLength(100)
            .WithMessage(
                "Email can't be longer than 100 characters. In case this is your real email contact administrator.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(AccountValidationMessages.PasswordRequired)
            .MinimumLength(8).WithMessage(AccountValidationMessages.PasswordTooShort)
            .MaximumLength(128).WithMessage(AccountValidationMessages.PasswordTooLong)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one non-alphanumeric character (e.g., !, @, #).");
    }
}
