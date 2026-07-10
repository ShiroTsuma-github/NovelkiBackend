namespace Infrastructure.BookCovers;

public sealed class BookCoverOptions
{
    public string StorageRoot { get; set; } = "storage/covers";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    public string UserAgent { get; set; } = "NovelkiBackend/1.0";
    public BookCoverS3Options? S3 { get; set; }
}

public sealed class BookCoverS3Options
{
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Bucket { get; set; }
}
