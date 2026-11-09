namespace SmartParking.Contracts.AccountAccess;

public static class AccountAccessEndpoints
{
    public const string Requests = "user-account-access-v1";
    public const string Decisions = "operations-account-access-results-v1";
}

public sealed record AccountAccessRequestV1(Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation,
    DateTimeOffset AdmittedAt, DateTimeOffset AttemptDeadline, DateTimeOffset OverallDeadline);

public sealed record AccountAccessDecisionV1(Guid DecisionId, Guid WorkflowId, Guid AttemptId, int AttemptNumber,
    Guid UserId, Guid AccountId, string Role, string Operation, DateTimeOffset DecidedAt, bool Allowed, string Code);
