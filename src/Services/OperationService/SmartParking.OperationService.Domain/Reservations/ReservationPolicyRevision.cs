namespace SmartParking.OperationService.Domain.Reservations;

public sealed record ReservationPolicyRevision(Guid Id, int Version, Guid? TenantId, Guid? SiteId,
    IReadOnlyDictionary<string, int> Values, IReadOnlyDictionary<string, PolicyBound> OverrideBounds,
    bool? ArrivalLateToleranceEnabled, string? CancellationTimezone);
