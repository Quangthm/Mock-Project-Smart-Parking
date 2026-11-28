namespace SmartParking.FacilityService.API.Controllers;

public sealed record CreateEmergencyRestrictionRequest(
    string Mode,
    string Scope,
    Guid? UnitId,
    Guid? SlotId,
    string Reason,
    DateTimeOffset? EffectiveUntil);