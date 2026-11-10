namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public sealed record ActiveVehicleLocation(
    Guid SessionId,
    Guid SiteId,
    string SiteCode,
    Guid? CurrentSlotId,
    Guid? CurrentSpatialUnitId,
    string? AllocationStatus,
    string? SlotCode,
    Guid? ZoneId,
    string? ZoneCode,
    string? FloorCode,
    DateTimeOffset? RecordedAt);

public interface IVehicleLocationReadStore
{
    Task<ActiveVehicleLocation?> FindActiveForAccountAsync(Guid accountId, CancellationToken ct);
}