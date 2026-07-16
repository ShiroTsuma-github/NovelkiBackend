namespace Domain.Exceptions;

public class TokenGeneratorFailedException : Exception
{
    public TokenGeneratorFailedException()
        : base("Token Generator Failed. Incorrect User Data")
    {
    }
}
