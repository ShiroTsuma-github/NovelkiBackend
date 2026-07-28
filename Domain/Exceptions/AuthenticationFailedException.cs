namespace Domain.Exceptions;

public sealed class AuthenticationFailedException : Exception
{
    public const string PublicMessage = "Invalid username or password";

    public AuthenticationFailedException()
        : base(PublicMessage)
    {
    }
}
