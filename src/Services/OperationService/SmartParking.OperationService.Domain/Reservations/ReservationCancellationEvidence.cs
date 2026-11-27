namespace SmartParking.OperationService.Domain.Reservations;

// Evidence owned by Operations, not an audit log. Future cancellation handlers must persist it
// in the cancellation transaction under the same account guard as new-booking admission.
public sealed class ReservationCancellationEvidence
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTimeOffset CancelledAt { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset? RestrictedUntil { get; private set; }
    public string PolicySnapshot { get; private set; } = "";
    private ReservationCancellationEvidence() { }
    public static ReservationCancellationEvidence Record(Guid id, Guid reservation, Guid account,
        DateTimeOffset at, string reason, DateTimeOffset? restrictedUntil, string policySnapshot)
    {
        if (id == Guid.Empty || reservation == Guid.Empty || account == Guid.Empty ||
            reason is not ("VOLUNTARY" or "FACILITY_FAILURE" or "ADMIN_CORRECTION") ||
            restrictedUntil <= at || string.IsNullOrWhiteSpace(policySnapshot) ||
            reason != "VOLUNTARY" && restrictedUntil is not null)
            throw new ArgumentException("Invalid cancellation evidence.");
        return new() { Id = id, ReservationId = reservation, AccountId = account, CancelledAt = at,
            Reason = reason, RestrictedUntil = restrictedUntil, PolicySnapshot = policySnapshot };
    }
}
