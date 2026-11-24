using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Helpers;

// Pure projection of live emergency restrictions onto the operator layout.
// A restriction hides capacity immediately; a vehicle already parked inside is
// never displaced and keeps its OCCUPIED projection.
public sealed class OperationalRestrictionOverlay
{
    private const int MaxDepth = 64;

    private readonly IReadOnlyList<EmergencyRestriction> _restrictions;
    private readonly IReadOnlyDictionary<Guid, Guid?> _unitParents;
    private readonly DateTime _now;

    public OperationalRestrictionOverlay(
        IReadOnlyList<EmergencyRestriction> restrictions,
        DateTime now,
        IReadOnlyDictionary<Guid, Guid?> unitParents)
    {
        _restrictions = restrictions;
        _now = now;
        _unitParents = unitParents;
    }

    // Applies the restriction overlay to a projected slot. Returns the slot unchanged
    // when a vehicle occupies it or no live restriction covers it.
    public SlotVisualStateDto Apply(SlotVisualStateDto slot, Guid slotUnitId)
    {
        if (slot.PhysicalState == PhysicalState.OCCUPIED) return slot;
        if (Find(slot.SlotId, slotUnitId) is not { } restriction) return slot;
        return slot with
        {
            PhysicalState = restriction.Mode == EmergencyRestrictionRules.ForcedMaintenanceMode
                ? PhysicalState.MAINTENANCE
                : PhysicalState.UNAVAILABLE,
            IsBookable = false,
        };
    }

    public bool IsBlocked(Guid slotId, Guid slotUnitId) => Find(slotId, slotUnitId) is not null;

    private EmergencyRestriction? Find(Guid slotId, Guid slotUnitId)
        => _restrictions.FirstOrDefault(restriction => IsLive(restriction) && Covers(restriction, slotId, slotUnitId));

    private bool IsLive(EmergencyRestriction restriction)
        => restriction.ReleasedAt is null
            && (restriction.EffectiveUntil is not { } until || until.UtcDateTime > _now);

    private bool Covers(EmergencyRestriction restriction, Guid slotId, Guid slotUnitId)
    {
        if (restriction.Scope == EmergencyRestrictionRules.SiteScope) return true;
        if (restriction.SlotId is { } scopedSlot && scopedSlot == slotId) return true;
        if (restriction.Scope != EmergencyRestrictionRules.UnitScope) return false;
        return restriction.UnitId is { } unit && IsInSubtree(slotUnitId, unit);
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
}