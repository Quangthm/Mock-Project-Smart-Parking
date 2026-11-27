namespace SmartParking.Contracts.ReservationAdmission;

public sealed record SlotCheckV1(Guid WorkflowId, Guid AttemptId, Guid AccountId, string Fingerprint,
    Guid RequestedSlotId, DateTimeOffset Start, DateTimeOffset End);
