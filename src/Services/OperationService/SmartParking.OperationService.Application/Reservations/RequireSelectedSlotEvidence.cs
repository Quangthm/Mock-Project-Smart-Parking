using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

internal static class RequireSelectedSlotEvidence
{
    public static void Validate(ReservationRow proposed, FacilityResourceSnapshot snapshot)
    {
        if (snapshot.Booking.TenantId != proposed.TenantId ||
            snapshot.Booking.SiteId != proposed.SiteId || snapshot.Booking.SlotId != proposed.TargetSlotId ||
            !snapshot.Booking.EligibleCarSlot || !snapshot.Booking.ParticipatesInStructureGuard)
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected slot is unavailable.");
        // The current Facility protocol still delivers a snapshot. Only the slot being evaluated is relevant.
        var selectedSlotId = proposed.ActiveClaimSlotId ?? proposed.TargetSlotId;
        var selected = snapshot.Resources.Where(r => r.SlotId == selectedSlotId).ToArray();
        if (selected.Length > 1)
            throw new ReservationGateException(503, "CAPACITY_CONFIGURATION_REQUIRED", "The selected slot evidence is inconsistent.");
        if (selected.Length == 0 || !selected[0].Active || !selected[0].Reachable || selected[0].FixedBackup ||
            selected[0].VehicleType != proposed.VehicleType ||
            !(selected[0].CompatibleVehicleTypes?.Contains(proposed.VehicleType) ?? proposed.VehicleType == "CAR"))
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected slot is unavailable.");
    }
}
