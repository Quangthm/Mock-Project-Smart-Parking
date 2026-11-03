namespace SmartParking.FacilityService.Domain.Entities;

public sealed class SupportTicketEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid UserId { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? BookingId { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
