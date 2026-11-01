namespace SmartParking.FacilityService.Application;

// Pure, side-effect-free mapping between persisted state strings and the wire contract enums.
public static class OperationalStateMapping
{
    public static PhysicalState MapPhysicalState(string? raw) => raw?.Trim() switch
    {
        "AVAILABLE" => PhysicalState.AVAILABLE,
        "OCCUPIED" => PhysicalState.OCCUPIED,
        "MAINTENANCE" => PhysicalState.MAINTENANCE,
        "UNAVAILABLE" => PhysicalState.UNAVAILABLE,
        "UNKNOWN" => PhysicalState.UNKNOWN,
        // Fail closed: null, empty, wrong-case, or any unrecognised value must never become AVAILABLE.
        _ => PhysicalState.UNKNOWN
    };

    public static ReservationState MapReservationState(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value)) return ReservationState.UNRESERVED;
        return value switch
        {
            "UNRESERVED" => ReservationState.UNRESERVED,
            "RESERVED" => ReservationState.RESERVED,
            "PROTECTED" => ReservationState.PROTECTED,
            "BACKUP" => ReservationState.BACKUP,
            // Fail closed: an unrecognised non-empty value must never become UNRESERVED.
            _ => ReservationState.BACKUP
        };
    }
}
