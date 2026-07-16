namespace Domain.Exceptions;

public class IdentityOperationFailedException : Exception
{
    public const string DefaultMessage = "Identity operation failed.";

    public IdentityOperationFailedException(IEnumerable<string> errors)
        : base(DefaultMessage)
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyCollection<string> Errors { get; }
}
