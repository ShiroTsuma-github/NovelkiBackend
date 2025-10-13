namespace Domain.Exceptions;

public class UsernameTakenException : Exception
{
    public UsernameTakenException(string identifier)
    : base($"Account with username '{identifier}' already exists.")
    {
    }
}
