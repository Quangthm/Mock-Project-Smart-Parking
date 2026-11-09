namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>Durable admission/authorization state, separate from the reservation lifecycle.</summary>
public sealed class ReservationRequest
{
    private ReservationRequest() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string Operation { get; private set; } = "CREATE_RESERVATION";
    public string IntentJson { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string State { get; private set; } = "PENDING_AUTHORIZATION";
    public string? Code { get; private set; }
    public DateTimeOffset AdmittedAt { get; private set; }
    public DateTimeOffset OverallDeadline { get; private set; }
    public Guid AttemptId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset AttemptStartedAt { get; private set; }
    public DateTimeOffset AttemptDeadline { get; private set; }
    public DateTimeOffset NextActionAt { get; private set; }
    public Guid? DecisionId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public bool AwaitingReply { get; private set; }

    public static ReservationRequest Admit(Guid userId, Guid accountId, Guid key,
        string intentJson, string fingerprint, DateTimeOffset now)
    {
        if (userId == Guid.Empty || accountId == Guid.Empty || key == Guid.Empty ||
            string.IsNullOrWhiteSpace(intentJson) || string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("Admission requires identity and normalized intent.");
        var request = new ReservationRequest
        {
            Id = Guid.NewGuid(), UserId = userId, AccountId = accountId, IdempotencyKey = key,
            IntentJson = intentJson, Fingerprint = fingerprint, AdmittedAt = now,
            OverallDeadline = now.AddSeconds(90)
        };
        request.StartAttempt(now);
        return request;
    }

    /// <returns>True only when one new authorization request must be recorded in the outbox.</returns>
    public bool Advance(DateTimeOffset now)
    {
        if (State != "PENDING_AUTHORIZATION" || now < NextActionAt) return false;
        if (now >= OverallDeadline)
        {
            FailDependency("AUTHORIZATION_UNAVAILABLE");
            return false;
        }
        if (AwaitingReply)
        {
            AwaitingReply = false; // fence the timed-out attempt even before the next attempt starts
            if (AttemptNumber == 3) FailDependency("AUTHORIZATION_UNAVAILABLE");
            else NextActionAt = now.AddSeconds(AttemptNumber == 1 ? 10 : 30);
            return false;
        }
        StartAttempt(now);
        return true;
    }

    // The approved reservation exception replays one workflow decision in a NEW delivery envelope.
    // Original DecidedAt may precede this attempt; envelope ID and receipt deadline still fence delivery.
    public bool AcceptDecision(Guid attemptId, int attemptNumber, Guid decisionId,
        DateTimeOffset decidedAt, bool allowed, DateTimeOffset receivedAt)
    {
        if (State != "PENDING_AUTHORIZATION" || !AwaitingReply || attemptId != AttemptId ||
            attemptNumber != AttemptNumber || decisionId == Guid.Empty || decidedAt < AdmittedAt ||
            decidedAt > receivedAt || decidedAt >= AttemptDeadline || receivedAt >= AttemptDeadline ||
            receivedAt >= OverallDeadline) return false;
        DecisionId = decisionId;
        DecidedAt = decidedAt;
        AwaitingReply = false;
        State = allowed ? "PREPARING" : "DENIED";
        Code = allowed ? null : "CURRENT_ACCESS_DENIED";
        return true;
    }

    public void BlockUnavailableFinalization()
    {
        if (State != "PREPARING") throw new InvalidOperationException("A current Allowed decision is required.");
        // Explicit deployment gate: no quote/coverage producer or D4c commit proof is supplied yet.
        FailDependency("RESERVATION_FINALIZATION_NOT_READY");
    }

    private void StartAttempt(DateTimeOffset now)
    {
        AttemptId = Guid.NewGuid();
        AttemptNumber++;
        AttemptStartedAt = now;
        AttemptDeadline = now.AddSeconds(5) < OverallDeadline ? now.AddSeconds(5) : OverallDeadline;
        NextActionAt = AttemptDeadline;
        AwaitingReply = true;
    }

    private void FailDependency(string code)
    {
        State = "FAILED_DEPENDENCY";
        Code = code;
        AwaitingReply = false;
    }
}
