namespace Domain.Entities;

public sealed class StorageCleanupQueueItem : BaseEntity
{
    public required string StoragePath { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
