namespace Application.Features.AccountFeatures.Validators;

internal static class AccountValidationMessages
{
    public const string UsernameTooShort = "Username must be at least 3 characters long.";
    public const string UsernameTooLong = "Username can't be longer than 32 characters.";
    public const string PasswordRequired = "Password is required.";
    public const string PasswordTooShort = "Password must be at least 8 characters long.";
    public const string PasswordTooLong = "Password can't be longer than 128 characters.";
    public const string InvalidEmail = "A valid email address is required.";
}
