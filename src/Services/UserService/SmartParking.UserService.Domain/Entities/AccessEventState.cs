namespace SmartParking.UserService.Domain.Entities;

public sealed class AccessEventOutbox
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
}
public sealed class AccessEventInbox
{
    public Guid Id { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
public sealed class AccessProjection
{
    public Guid UserId { get; set; }
    public long Version { get; set; }
    public string Status { get; set; } = "";
}

