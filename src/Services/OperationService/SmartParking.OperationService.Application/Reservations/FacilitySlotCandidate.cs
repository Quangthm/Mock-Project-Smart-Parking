namespace SmartParking.OperationService.Application.Reservations;

// One facility bay that could replace a closed slot. Facility resolves the physical facts; this
// record only carries what the reconciliation needs to pick a safe replacement.
public sealed record FacilitySlotCandidate(
    Guid SlotId,
    Guid? SpatialUnitId,
    string VehicleType,
    bool Eligible,
    bool Active,
    bool Reachable,
    bool Backup,
    IReadOnlyList<Guid>? AncestorUnitIds = null,
    IReadOnlyList<string>? CompatibleVehicleTypes = null)
{
    public bool Supports(string vehicleType) =>
        string.Equals(VehicleType, vehicleType, StringComparison.OrdinalIgnoreCase) ||
        (CompatibleVehicleTypes?.Any(type =>
            string.Equals(type, vehicleType, StringComparison.OrdinalIgnoreCase)) ?? false);
}