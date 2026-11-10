using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetCurrentVehicleLocation;

public sealed class GetCurrentVehicleLocationQueryHandler(IVehicleLocationReadStore store)
    : IRequestHandler<GetCurrentVehicleLocationQuery, VehicleLocationResponseDto?>
{
    public async Task<VehicleLocationResponseDto?> Handle(
        GetCurrentVehicleLocationQuery request, CancellationToken ct)
    {
        var fact = await store.FindActiveForAccountAsync(request.AccountId, ct);
        if (fact is null)
            return null;

        RecordedLocationDto location;
        DateTimeOffset? recordedAt;
        if (fact.CurrentSlotId is not null)
        {
            location = new RecordedLocationDto(
                "SLOT", fact.CurrentSlotId, fact.SlotCode, fact.ZoneId, fact.ZoneCode, fact.FloorCode);
            recordedAt = fact.RecordedAt;
        }
        else if (fact.CurrentSpatialUnitId is not null)
        {
            location = new RecordedLocationDto(
                "ZONE", null, null, fact.CurrentSpatialUnitId, fact.ZoneCode, fact.FloorCode);
            recordedAt = fact.RecordedAt;
        }
        else
        {
            location = new RecordedLocationDto("UNKNOWN", null, null, null, null, null);
            recordedAt = null;
        }

        var allocationStatus = fact.AllocationStatus is "RESERVED" or "OCCUPIED"
            ? fact.AllocationStatus
            : null;

        var freshness = new FreshnessInfoDto(DateTimeOffset.UtcNow, false, 30, "OPERATIONS");

        return new VehicleLocationResponseDto(
            fact.SessionId,
            fact.SiteId,
            fact.SiteCode,
            allocationStatus,
            location,
            recordedAt,
            freshness);
    }
}