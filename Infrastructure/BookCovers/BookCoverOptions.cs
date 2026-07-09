namespace Infrastructure.BookCovers;

public sealed class BookCoverOptions
{
    public string StorageRoot { get; set; } = "storage/covers";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    public string UserAgent { get; set; } = "NovelkiBackend/1.0";
}
