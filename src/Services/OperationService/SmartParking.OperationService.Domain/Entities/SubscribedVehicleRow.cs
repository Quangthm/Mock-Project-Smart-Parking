using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Domain.Entities;

public sealed class SubscribedVehicleRow
{
    public Guid SubscriptionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public DateTimeOffset ActiveAt { get; set; }
    public DateTimeOffset ExpireAt { get; set; }

    public bool Covers(string plateNumber, DateTimeOffset at) =>
        TenantId != Guid.Empty && SiteId != Guid.Empty &&
        ActiveAt <= at && at < ExpireAt &&
        GateRules.NormalizePlate(LicensePlate) == GateRules.NormalizePlate(plateNumber);
}
