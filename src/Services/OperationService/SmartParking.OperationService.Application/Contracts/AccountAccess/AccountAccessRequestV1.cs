// Service-local wire contract; no source/project reference to another service.
// Namespace/type names are retained for MassTransit wire compatibility.
namespace SmartParking.Contracts.AccountAccess;

// Reservation-specific: attempts request/replay ONE persisted workflow decision, not fresh evaluations.
public sealed record AccountAccessRequestV1(Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation,
    DateTimeOffset AdmittedAt, DateTimeOffset AttemptDeadline, DateTimeOffset OverallDeadline);
