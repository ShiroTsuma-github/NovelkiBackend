namespace Domain.Exceptions;

public class IdentityOperationFailedException : Exception
{
    public IdentityOperationFailedException(IEnumerable<string> errors)
        : base("Identity operation failed.")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyCollection<string> Errors { get; }
}
