namespace SmartParking.Contracts.ReservationAdmission;

public sealed record PlateCheckV1(Guid WorkflowId, Guid AttemptId, Guid AccountId, string Fingerprint, string CanonicalPlate);
