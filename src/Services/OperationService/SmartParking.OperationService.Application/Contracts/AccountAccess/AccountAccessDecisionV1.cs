// Service-local wire contract; no source/project reference to another service.
// Namespace/type names are retained for MassTransit wire compatibility.
namespace SmartParking.Contracts.AccountAccess;

// DecisionId/DecidedAt are stable across retries. AttemptId/AttemptNumber echo the current delivery.
public sealed record AccountAccessDecisionV1(Guid DecisionId, Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation, DateTimeOffset DecidedAt, bool Allowed, string Code);
