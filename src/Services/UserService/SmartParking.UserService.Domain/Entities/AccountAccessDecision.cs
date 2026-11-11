namespace SmartParking.UserService.Domain.Entities;

public sealed record CurrentAccountAuthority(bool UserActive, bool AccountActive, bool DriverRoleExists, string[]? Permissions)
{
    public bool Allows(string operation) => UserActive && AccountActive && DriverRoleExists &&
        operation is "CREATE_RESERVATION" or "CANCEL_OWN_RESERVATION" &&
        (Permissions is null || Permissions.Contains(operation == "CREATE_RESERVATION" ? "reservation.create" : "reservation.cancel", StringComparer.Ordinal));
}

public sealed class AccountAccessDecision
{
    private AccountAccessDecision() { }
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public string Role { get; private set; } = "";
    public string Operation { get; private set; } = "";
    public string RequestFingerprint { get; private set; } = "";
    public bool Allowed { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    public static AccountAccessDecision Evaluate(Guid workflowId,
        Guid userId, Guid accountId, string role, string operation, string fingerprint,
        CurrentAccountAuthority? authority, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId,
            UserId = userId, AccountId = accountId, Role = role, Operation = operation,
            RequestFingerprint = fingerprint, DecidedAt = now,
            Allowed = role == "driver" && authority?.Allows(operation) == true
        };
}
