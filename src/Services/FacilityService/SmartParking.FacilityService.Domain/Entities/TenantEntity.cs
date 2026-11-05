namespace SmartParking.FacilityService.Domain.Entities;

public sealed class TenantEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? DeletedAt { get; set; }
}
