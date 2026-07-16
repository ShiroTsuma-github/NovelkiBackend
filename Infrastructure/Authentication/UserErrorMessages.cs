namespace Infrastructure.Authentication;

internal static class UserErrorMessages
{
    public const string RequiredIdUnavailable =
        "Attempted to access required user ID when the user was not logged in or the claim was missing.";
}
