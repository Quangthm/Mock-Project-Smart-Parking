using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

/// <summary>
/// Resolves the primary vehicle category of a slot write. The primary is the projection column and the
/// only pool a slot is counted in, so it is never inferred from a multi-category set: the caller must
/// declare it, and a declared legacy <c>vehicleType</c> must agree with it.
/// </summary>
public static class SlotCompatibility
{
    public static VehicleType Primary(VehicleType? single, VehicleType? primary, IReadOnlyList<VehicleType>? vehicleTypes)
    {
        if (single is { } legacy && primary is { } declaredPrimary && legacy != declaredPrimary)
            throw new StructureException("INVALID_STRUCTURE", "vehicleType and primaryVehicleType must agree.");
        if (primary is { } explicitPrimary) return explicitPrimary;
        if (single is { } legacyOnly) return legacyOnly;
        // A one-element set has exactly one possible primary; two or more never get an inferred one.
        if (vehicleTypes is [var only]) return only;
        throw new StructureException("INVALID_STRUCTURE",
            "Declare a primary vehicle type when a slot has multiple vehicle types.");
    }
}
