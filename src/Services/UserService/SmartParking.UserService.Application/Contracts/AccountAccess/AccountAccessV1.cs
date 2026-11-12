// Service-local wire contract; no source/project reference to another service.
// Namespace/type names are retained for MassTransit wire compatibility.
namespace SmartParking.Contracts.AccountAccess;

public static class AccountAccessEndpoints
{
    public const string Requests = "user-account-access-v1";
    public const string Decisions = "operations-account-access-results-v1";
}

// Reservation-specific: attempts request/replay ONE persisted workflow decision, not fresh evaluations.
public sealed record AccountAccessRequestV1(Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation,
    DateTimeOffset AdmittedAt, DateTimeOffset AttemptDeadline, DateTimeOffset OverallDeadline);

// DecisionId/DecidedAt are stable across retries. AttemptId/AttemptNumber echo the current delivery.
public sealed record AccountAccessDecisionV1(Guid DecisionId, Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation, DateTimeOffset DecidedAt, bool Allowed, string Code);
