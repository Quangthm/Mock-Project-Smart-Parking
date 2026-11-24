namespace SmartParking.FacilityService.Domain;

public static class EmergencyRestrictionRules
{
    public const string EmergencyMode = "EMERGENCY";
    public const string ForcedMaintenanceMode = "FORCED_MAINTENANCE";

    public const string SiteScope = "SITE";
    public const string UnitScope = "UNIT";
    public const string SlotScope = "SLOT";

    private const int MaxReasonLength = 2000;

    /// <summary>
    /// Validates and normalizes an emergency restriction request. Scope and mode are upper-cased
    /// so callers may send either casing; the reason is trimmed.
    /// </summary>
    public static (string Mode, string Scope, string Reason) ValidateCreate(
        string? mode,
        string? scope,
        Guid? unitId,
        Guid? slotId,
        string? reason,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil)
    {
        var normalizedMode = (mode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedMode is not (EmergencyMode or ForcedMaintenanceMode))
            throw new StructureException("INVALID_STRUCTURE",
                "Mode must be EMERGENCY or FORCED_MAINTENANCE.");

        var normalizedScope = (scope ?? string.Empty).Trim().ToUpperInvariant();
        switch (normalizedScope)
        {
            case SiteScope:
                if (unitId is not null || slotId is not null)
                    throw new StructureException("INVALID_STRUCTURE",
                        "A site-wide restriction cannot target a unit or a slot.");
                break;
            case UnitScope:
                if (unitId is null || slotId is not null)
                    throw new StructureException("INVALID_STRUCTURE",
                        "A unit restriction requires a unit and cannot target a slot.");
                break;
            case SlotScope:
                if (unitId is null || slotId is null)
                    throw new StructureException("INVALID_STRUCTURE",
                        "A slot restriction requires both a unit and a slot.");
                break;
            default:
                throw new StructureException("INVALID_STRUCTURE",
                    "Scope must be SITE, UNIT, or SLOT.");
        }

        var normalizedReason = (reason ?? string.Empty).Trim();
        if (normalizedReason.Length is 0 or > MaxReasonLength)
            throw new StructureException("INVALID_STRUCTURE",
                "A reason of 1 to 2,000 characters is required.");

        if (effectiveUntil is { } until && until <= effectiveFrom)
            throw new StructureException("INVALID_STRUCTURE",
                "A restriction cannot end at or before it starts.");

        return (normalizedMode, normalizedScope, normalizedReason);
    }
}