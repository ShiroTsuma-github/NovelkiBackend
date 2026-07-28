namespace Domain.Entities;

public sealed class BookSearchIndexQueueItem
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;
    public DateTimeOffset EnqueuedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
}
