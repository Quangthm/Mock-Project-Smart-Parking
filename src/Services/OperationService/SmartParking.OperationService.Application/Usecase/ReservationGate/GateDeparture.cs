using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

internal static class GateDeparture
{
    public static async Task RecordAsync(ParkingSessionRow session, ISlotAllocationRepository allocations,
        DateTimeOffset departedAt, string? imageData, CancellationToken ct)
    {
        session.RecordDeparture(departedAt, imageData);
        var active = await allocations.FindActiveForVisitAsync(
            session.TenantId, session.SiteId, session.Id, session.ReservationId, ct);
        foreach (var allocation in active)
            allocation.Release(departedAt);
    }

    public static Task ReleaseScannedSlotAsync(ParkingSessionRow session, IOperatorGateSlotClaims gateSlots, CancellationToken ct) =>
        session.LocationScannedAt is not null && session.CurrentSlotId is { } slotId
            ? gateSlots.ReleaseAsync(session.TenantId, session.SiteId, slotId, ct)
            : Task.CompletedTask;
}
