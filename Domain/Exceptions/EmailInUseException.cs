namespace Domain.Exceptions;

public class EmailInUseException : Exception
{
    public EmailInUseException(string identifier)
        : base($"The account with email {identifier} already exists.")
    {
    }
}
