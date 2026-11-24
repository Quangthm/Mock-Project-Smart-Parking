using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Helpers;

// Pure projection of maintenance windows onto the operator layout (DE-3/DE-13).
// Only a window that is live, currently covering `now` and acknowledged in
// Operations may hide a slot. A parked vehicle is never rewritten: an OCCUPIED
// slot stays OCCUPIED even while its zone is closed for maintenance.
public sealed class OperationalMaintenanceOverlay
{
    private const string AcknowledgedState = "acknowledged";
    private const int MaxDepth = 64;

    private readonly IReadOnlyList<MaintenanceWindow> _windows;
    private readonly IReadOnlyDictionary<Guid, Guid?> _unitParents;
    private readonly DateTime _now;

    public OperationalMaintenanceOverlay(
        IReadOnlyList<MaintenanceWindow> windows,
        string siteStatus,
        DateTime now,
        IReadOnlyDictionary<Guid, Guid?> unitParents)
    {
        _windows = windows;
        _unitParents = unitParents;
        _now = now;

        // No maintenance window at all keeps ServiceAvailability null so the v1.2.0
        // wire shape is unchanged for sites that never scheduled maintenance.
        if (windows.Count == 0)
        {
            ServiceAvailability = null;
            return;
        }

        var live = windows.Where(window => window.CancelledAt is null).ToArray();
        var covering = live.Where(window => MaintenanceWindowState.Covers(window, now)).ToArray();
        ServiceAvailability = new OperationalServiceAvailabilityDto(
            MaintenanceWindowState.Derive(windows, siteStatus, now),
            ToOffset(MaintenanceWindowState.ClosedUntil(windows, now)),
            covering.Select(window => window.Reason).FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason)),
            covering
                .Select(window => new OperationalMaintenanceWindowDto(
                    window.Id,
                    window.UnitId,
                    window.SlotId,
                    ToOffset(window.Start)!.Value,
                    ToOffset(window.End)!.Value,
                    window.Reason,
                    window.EnforcementState))
                .ToArray());
    }

    public OperationalServiceAvailabilityDto? ServiceAvailability { get; }

    // Applies the maintenance overlay to a projected slot. Returns the slot unchanged
    // when it is occupied by a vehicle or not covered by an enforced window.
    public SlotVisualStateDto Apply(SlotVisualStateDto slot, Guid slotUnitId)
    {
        if (slot.PhysicalState == PhysicalState.OCCUPIED) return slot;
        if (!IsBlocked(slot.SlotId, slotUnitId)) return slot;
        return slot with { PhysicalState = PhysicalState.MAINTENANCE, IsBookable = false };
    }

    public bool IsBlocked(Guid slotId, Guid slotUnitId)
        => _windows.Any(window => IsEnforcedNow(window) && Covers(window, slotId, slotUnitId));

    private bool IsEnforcedNow(MaintenanceWindow window)
        => window.EnforcementState == AcknowledgedState && MaintenanceWindowState.Covers(window, _now);

    private bool Covers(MaintenanceWindow window, Guid slotId, Guid slotUnitId)
    {
        if (window.UnitId is null && window.SlotId is null) return true; // site-wide
        if (window.SlotId is { } scopedSlot) return scopedSlot == slotId;
        if (window.UnitId is not { } unit) return false;
        return IsInSubtree(slotUnitId, unit);
    }

    private bool IsInSubtree(Guid unitId, Guid ancestorId)
    {
        var current = unitId;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (current == ancestorId) return true;
            if (!_unitParents.TryGetValue(current, out var parent) || parent is not { } next) return false;
            current = next;
        }
        return false;
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value is { } instant ? new DateTimeOffset(DateTime.SpecifyKind(instant, DateTimeKind.Utc)) : null;
}