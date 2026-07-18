namespace Domain.Exceptions;

public sealed class CannotDeleteCurrentAccountException : Exception
{
    public CannotDeleteCurrentAccountException() : base(
        "The currently signed-in administrator account cannot be deleted.")
    {
    }
}
