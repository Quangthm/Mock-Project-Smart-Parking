namespace SmartParking.OperationService.Domain.Entities;

public sealed class SearchFacilityProjectionRow
{
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string[] SupportedVehicleTypes { get; set; } = [];
    public bool IsOpen { get; set; }
    public string OperationalStatus { get; set; } = string.Empty;
    public int CachedAvailableSpaces { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
}
