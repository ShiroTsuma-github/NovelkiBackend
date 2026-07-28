namespace Infrastructure.BookSearch;

using NpgsqlTypes;

internal static class BookSearchDbFunctions
{
    public static bool HasCloseLexeme(NpgsqlTsVector searchVector, string term)
    {
        throw new NotSupportedException(
            $"{nameof(HasCloseLexeme)} is intended for use in translated database queries only.");
    }
}
