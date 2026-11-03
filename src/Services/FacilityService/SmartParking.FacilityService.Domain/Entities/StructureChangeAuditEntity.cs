namespace SmartParking.FacilityService.Domain.Entities;

public sealed class StructureChangeAuditEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? OperationId { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
