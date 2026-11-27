namespace SmartParking.OperationService.Domain.Reservations;

public sealed record AvailabilityDecision(string? Rejection)
{
    public bool Indeterminate => Rejection is "RESERVATION_QUOTA_CONFIGURATION_INVALID" or "OCCUPANCY_RECONCILIATION_REQUIRED";
    public bool Allowed => Rejection is null;
}
