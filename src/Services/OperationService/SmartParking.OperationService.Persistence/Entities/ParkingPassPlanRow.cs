namespace SmartParking.OperationService.Persistence.Entities;

public sealed class ParkingPassPlanRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string EntitlementType { get; set; } = "WHEN_SPACE_AVAILABLE";
    public bool IsEnabled { get; set; }
    public string Status { get; set; } = "DRAFT";
    public int ActiveRevision { get; set; }
    public int? DraftRevision { get; set; }
    public string? DraftPayload { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
