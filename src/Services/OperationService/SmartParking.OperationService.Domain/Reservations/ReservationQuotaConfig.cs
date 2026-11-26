namespace SmartParking.OperationService.Domain.Reservations;

public sealed class ReservationQuotaConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string VehicleType { get; set; } = "";
    public int MaxReservationQuota { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
