namespace Domain.Entities;

public sealed class ReadingTimeSetting : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;
    public decimal MinutesPerChapter { get; set; }
}
