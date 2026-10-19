using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartParking.ReservationService.Application;

namespace SmartParking.ReservationService.Infrastructure;

public sealed class ParkingGateSlotClient(HttpClient client) : IOperatorGateSlotClaims
{
    public async Task<IOperatorGateSlotLease> ClaimAvailableAsync(
        Guid tenantId,
        Guid siteId,
        string vehicleType,
        Guid? preferredSlotId,
        Guid? targetUnitId,
        bool allowReservedSlot,
        CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"internal/gate-sites/{siteId}/slots/claim",
                new { tenantId, vehicleType, preferredSlotId, targetUnitId, allowReservedSlot },
                ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new ReservationGateException(409, "NO_AVAILABLE_SLOT", "No eligible parking slot is available.");
            if (!response.IsSuccessStatusCode)
                throw Unavailable();
            var slot = await response.Content.ReadFromJsonAsync<ClaimedParkingSlot>(ct)
                ?? throw Unavailable();
            if (slot.Id == Guid.Empty || string.IsNullOrWhiteSpace(slot.Code) ||
                slot.VehicleType is not ("CAR" or "MOTORCYCLE"))
                throw Unavailable();
            return new Lease(this, tenantId, siteId, slot);
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException ||
                                      error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    public async Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"internal/gate-sites/{siteId}/slots/{slotId}/release",
                new { tenantId },
                ct);
            if (!response.IsSuccessStatusCode)
                throw Unavailable();
        }
        catch (Exception error) when (error is HttpRequestException ||
                                      error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    private static ReservationGateException Unavailable() =>
        new(503, "PARKING_SLOT_UNAVAILABLE", "Parking Service could not safely update the gate slot.");

    private sealed class Lease(
        ParkingGateSlotClient owner,
        Guid tenantId,
        Guid siteId,
        ClaimedParkingSlot slot) : IOperatorGateSlotLease
    {
        private bool committed;

        public ClaimedParkingSlot Slot => slot;
        public void Commit() => committed = true;

        public async ValueTask DisposeAsync()
        {
            if (!committed)
                await owner.ReleaseAsync(tenantId, siteId, slot.Id, CancellationToken.None);
        }
    }
}
