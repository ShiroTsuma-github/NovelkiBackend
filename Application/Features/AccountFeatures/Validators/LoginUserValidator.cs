namespace Application.Features.AccountFeatures.Validators;

using Commands;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(128).WithMessage("Password can't be longer than 128 characters.");

        RuleFor(x => x)
            .Must(OnlyOneProvided)
            .WithMessage("Either Username or Email must be provided, but not both.");

        When(x => !string.IsNullOrWhiteSpace(x.Username), () =>
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required when provided.")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters long.")
                .MaximumLength(32).WithMessage("Username can't be longer than 32 characters.");
        });
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required when provided.")
                .EmailAddress().WithMessage("A valid email address is required.")
                .MaximumLength(100).WithMessage("Email can't be longer than 100 characters.");
        });
    }

    private bool OnlyOneProvided(LoginUserCommand command)
    {
        bool usernameProvided = !string.IsNullOrWhiteSpace(command.Username);
        bool emailProvided = !string.IsNullOrWhiteSpace(command.Email);
        return usernameProvided ^ emailProvided;
    }
}
