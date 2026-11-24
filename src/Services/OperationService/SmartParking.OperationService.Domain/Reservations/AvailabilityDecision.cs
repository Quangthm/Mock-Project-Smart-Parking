namespace SmartParking.OperationService.Domain.Reservations;

public sealed record AvailabilityDecision(string? Rejection)
{
    public bool Indeterminate => Rejection is "SCOPED_CAPACITY_EVIDENCE_REQUIRED" or
        "CAPACITY_CONFIGURATION_REQUIRED" or "OCCUPANCY_RECONCILIATION_REQUIRED";
    public bool Allowed => Rejection is null;
}
