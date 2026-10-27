namespace SmartParking.PaymentService.Domain.Entities;

public class Tariff
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VehicleType { get; set; } = "CAR";
    public int FreeParkingGraceMinutes { get; set; } = 15;
    public string TariffRulesJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
