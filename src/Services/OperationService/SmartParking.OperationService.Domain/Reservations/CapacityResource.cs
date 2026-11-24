namespace SmartParking.OperationService.Domain.Reservations;

public sealed record CapacityResource(Guid Id, Guid? Unit, IReadOnlyList<Guid> Ancestors, bool Usable, bool Backup,
    IReadOnlyList<string>? CompatibleCategories = null, IReadOnlyList<ReservationInterval>? Exclusions = null);
