namespace SmartParking.FacilityService.Domain;

public sealed record MaintenanceWindow(Guid Id, Guid TenantId, Guid SiteId, Guid? UnitId, Guid? SlotId,
    DateTime Start, DateTime End, string Reason, Guid CreatedBy, DateTime CreatedAt, DateTime? CancelledAt,
    string EnforcementState, Guid? EnforcementRef, DateTime? AcknowledgedAt);

// Service availability is derived at read time. Lifecycle status (ACTIVE/INACTIVE) is the Owner's
// separate decision and is never written by the window lifecycle.
public static class MaintenanceWindowState
{
    public const string Active = "ACTIVE";
    public const string Scheduled = "SCHEDULED";
    public const string Maintenance = "MAINTENANCE";
    public const string MaintenanceUncertain = "MAINTENANCE_UNCERTAIN";
    public const string Inactive = "INACTIVE";

    // Half-open [start, end): a window ending exactly at `now` no longer covers it.
    public static bool Covers(MaintenanceWindow window, DateTime now)
        => window.CancelledAt is null && window.Start <= now && now < window.End;

    public static string Derive(IReadOnlyList<MaintenanceWindow> windows, string siteStatus, DateTime now)
    {
        if (siteStatus is "INACTIVE" or "SUSPENDED") return Inactive;
        var live = windows.Where(window => window.CancelledAt is null).ToArray();
        var current = live.Where(window => Covers(window, now)).ToArray();
        if (current.Length > 0)
            return current.All(window => window.EnforcementState == "acknowledged") ? Maintenance : MaintenanceUncertain;
        return live.Any(window => window.Start > now) ? Scheduled : Active;
    }

    // End of the latest window covering `now`; null outside a window.
    public static DateTime? ClosedUntil(IReadOnlyList<MaintenanceWindow> windows, DateTime now)
    {
        var current = windows.Where(window => Covers(window, now)).Select(window => (DateTime?)window.End).ToArray();
        return current.Length == 0 ? null : current.Max();
    }
}

public static class MaintenanceWindowRules
{
    private static void Require(bool valid, string message, string code = "INVALID_STRUCTURE")
    { if (!valid) throw new StructureException(code, message); }

    public static void ValidateCreate(Guid? unitId, Guid? slotId, string? reason, DateTime start, DateTime end, DateTime now)
    {
        Require(start < end, "Maintenance window start must be before its end.");
        Require(start >= now, "Maintenance window cannot start in the past.");
        // DE-3: unit/slot-scoped windows cannot be enforced today, so they must not be stored.
        Require(unitId is null && slotId is null, "Scoped maintenance windows cannot be enforced yet; a window must cover the whole site.");
        Require(!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 2000, "Provide a reason (1-2000 characters).");
    }

    // Extend-only while the window has not ended. Shortening or moving the start would shrink an
    // already-announced closure, so it is a cancellation of the remaining interval plus a new window.
    public static DateTime Extend(DateTime currentStart, DateTime currentEnd, DateTime? start, DateTime? end, DateTime now)
    {
        Require(now < currentEnd, "Only a maintenance window that has not ended can be extended.", "STRUCTURE_IN_USE");
        Require(start is null || start == currentStart, "Moving the start is not an extension; cancel and create a new window.", "STRUCTURE_IN_USE");
        if (end is not { } newEnd) throw new StructureException("INVALID_STRUCTURE", "Provide the new end.");
        Require(newEnd > currentEnd, "A maintenance window can only be extended.", "STRUCTURE_IN_USE");
        return newEnd;
    }
}
