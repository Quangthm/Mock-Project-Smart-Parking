namespace SmartParking.OperationService.Domain.Entities;

public sealed class SiteCapacityPoolRow
{
    public void ApplyStructureCapacity(int total, int backup, DateTimeOffset now)
    {
        if (total < 0 || backup < 0 || backup > total || CurrentReservedCount > total)
            throw new SmartParking.OperationService.Domain.InvalidStructureEditException("Capacity cannot invalidate existing commitments.");
        TotalCapacity = total;
        MaxReservationQuota = Math.Min(MaxReservationQuota, total);
        BackupCount = backup;
        UpdatedAt = now;
    }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? SpatialUnitId { get; set; }
    public string VehicleType { get; set; } = "";
    public int TotalCapacity { get; set; }
    public int MaxReservationQuota { get; set; }
    public int CurrentReservedCount { get; set; }
    public int PendingPaymentCount { get; set; }
    public int OccupiedCount { get; set; }
    public int ProtectedCount { get; set; }
    public int BackupCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StructureEditHoldRow
{
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid Token { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StructureSiteStateRow
{
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public bool Active { get; set; }
}

public sealed class StructureReleaseReceiptRow
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Outcome { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; }
}
