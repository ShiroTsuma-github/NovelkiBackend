namespace Domain.Exceptions;

public class WrongPasswordException : Exception
{
    public WrongPasswordException()
        : base("Wrong Password")
    {
    }
}
