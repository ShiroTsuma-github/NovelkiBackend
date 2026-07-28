namespace Infrastructure.BookSearch;

using Npgsql;

public static class BookSearchConnectionSettings
{
    public const double WordSimilarityThreshold = 0.55;
    private const string WordSimilarityOption = "-c pg_trgm.word_similarity_threshold=0.55";

    public static string? Apply(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Options = string.IsNullOrWhiteSpace(builder.Options)
            ? WordSimilarityOption
            : $"{builder.Options.Trim()} {WordSimilarityOption}";
        return builder.ConnectionString;
    }
}
