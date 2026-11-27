namespace SmartParking.OperationService.Persistence.Entities;

public sealed class ParkingPassPlanRevisionRow
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public int RevisionNumber { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public string VehicleTypes { get; set; } = "[]";
    public string OrdinaryCoverageRules { get; set; } = "{}";
    public string ContractTimezone { get; set; } = "Asia/Ho_Chi_Minh";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? PublishNote { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? IdempotencyPayloadHash { get; set; }
    public Guid PublishedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
