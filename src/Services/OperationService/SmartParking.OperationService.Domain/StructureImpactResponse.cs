namespace SmartParking.OperationService.Domain;

public sealed record ReservationImpactItem(
    Guid Id,
    string Status,
    DateTimeOffset ExpectedStart,
    DateTimeOffset? ConfirmedAt,
    Guid? UnitId,
    Guid? SlotId);

public sealed record SessionImpactItem(
    Guid Id,
    Guid? SlotId);

public sealed record StructureImpactResponse(
    IReadOnlyList<ReservationImpactItem> Reservations,
    IReadOnlyList<SessionImpactItem> Sessions,
    string Priority = "expectedStartThenConfirmation",
    string Preference = "sameLotThenZone");
