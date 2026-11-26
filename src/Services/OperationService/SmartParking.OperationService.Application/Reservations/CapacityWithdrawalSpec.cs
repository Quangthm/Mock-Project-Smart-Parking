namespace SmartParking.OperationService.Application.Reservations;

internal sealed record CapacityWithdrawalSpec(
    Guid SourceId,
    Guid TenantId,
    Guid SiteId,
    string Scope,
    Guid? UnitId,
    IReadOnlyList<Guid> ScopedUnitIds,
    IReadOnlyList<Guid> AffectedSlotIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil);
