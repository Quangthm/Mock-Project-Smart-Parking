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
    public string State { get; private set; } = "CHECKING";
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
    public Guid? ReservationId { get; private set; }
    public long ProgressVersion { get; private set; } = 1;
    public void AdvanceProgressVersion() => ProgressVersion = checked(ProgressVersion + 1);
    public string? AccountCheck { get; private set; }
    public string? PlateCheck { get; private set; }
    public string? SlotCheck { get; private set; }
    public Guid? ResolvedSiteId { get; private set; }
    public Guid? ResolvedTenantId { get; private set; }
    public string? VehicleBindingState { get; private set; }
    public long? VehicleBindingRevision { get; private set; }
    public DateTimeOffset? VehicleBindingCheckedAt { get; private set; }
    public string? VehiclePlate { get; private set; }

    public static ReservationRequest AdmitQuote(Guid userId, Guid accountId, Guid key, string intentJson,
        string fingerprint, DateTimeOffset now)
    {
        if (userId == Guid.Empty || accountId == Guid.Empty || key == Guid.Empty ||
            string.IsNullOrWhiteSpace(intentJson) || string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("Admission requires identity and normalized intent.");
        var request = new ReservationRequest
        {
            Id = Guid.NewGuid(), UserId = userId, AccountId = accountId, IdempotencyKey = key,
            IntentJson = intentJson, Fingerprint = fingerprint, AdmittedAt = now,
            OverallDeadline = now.AddSeconds(90),
            AccountCheck = "PENDING", PlateCheck = "PENDING", SlotCheck = "PENDING"
        };
        request.StartAttempt(now);
        return request;
    }

    public void RecordPlate(bool eligible, string plate, long? revision, DateTimeOffset now)
    {
        if (State != "CHECKING" || PlateCheck != "PENDING") return;
        PlateCheck = eligible ? "SUCCEEDED" : "REJECTED";
        if (!eligible) { Reject("PLATE_INELIGIBLE"); return; }
        VehiclePlate = plate;
        VehicleBindingRevision = revision;
        VehicleBindingCheckedAt = now;
        CheckJoin();
    }

    public void RecordSlot(bool eligible, Guid? site, Guid? tenant)
    {
        if (State != "CHECKING" || SlotCheck != "PENDING") return;
        SlotCheck = eligible ? "SUCCEEDED" : "REJECTED";
        if (!eligible) { Reject("SLOT_INELIGIBLE"); return; }
        ResolvedSiteId = site;
        ResolvedTenantId = tenant;
        CheckJoin();
    }

    private void CheckJoin()
    {
        if (AccountCheck == "SUCCEEDED" && PlateCheck == "SUCCEEDED" && SlotCheck == "SUCCEEDED")
            State = "EVALUATING";
    }

    public void FailQuoteAdmission(string code)
    {
        if (State is not ("CHECKING" or "EVALUATING")) return;
        if (code == "ADMISSION_TIMED_OUT")
        {
            if (AccountCheck == "PENDING") AccountCheck = "FAILED_DEPENDENCY";
            if (PlateCheck == "PENDING") PlateCheck = "FAILED_DEPENDENCY";
            if (SlotCheck == "PENDING") SlotCheck = "FAILED_DEPENDENCY";
        }
        else if (code == "ACCOUNT_RESPONSE_INVALID") AccountCheck = "FAILED_DEPENDENCY";
        else if (code == "PLATE_RESPONSE_INVALID") PlateCheck = "FAILED_DEPENDENCY";
        else if (code is "FACILITY_UNAVAILABLE" or "FACILITY_RESPONSE_INVALID") SlotCheck = "FAILED_DEPENDENCY";
        FailDependency(code);
    }

    public void CompleteAdmission(Guid reservationId, bool confirmed)
    {
        if (reservationId != Id || State != "EVALUATING" || AccountCheck != "SUCCEEDED" ||
            PlateCheck != "SUCCEEDED" || SlotCheck != "SUCCEEDED" || DecisionId is null)
            throw new InvalidOperationException("All owner checks are required for a quote hold.");
        ReservationId = reservationId;
        State = confirmed ? "CONFIRMED" : "HOLD_CREATED";
        Code = null;
    }

    public void Defer(DateTimeOffset nextActionAt, string code)
    {
        NextActionAt = nextActionAt;
        Code = code;
    }

    public void Reject(string code)
    {
        State = "REJECTED";
        Code = code;
        SkipPendingChecks();
    }

    private void SkipPendingChecks()
    {
        if (AccountCheck == "PENDING") AccountCheck = "SKIPPED";
        if (PlateCheck == "PENDING") PlateCheck = "SKIPPED";
        if (SlotCheck == "PENDING") SlotCheck = "SKIPPED";
    }

    /// <returns>True only when one new authorization request must be recorded in the outbox.</returns>
    public bool Advance(DateTimeOffset now)
    {
        if (State == "CHECKING" && now >= OverallDeadline)
        {
            FailDependency("ADMISSION_TIMED_OUT");
            return false;
        }
        if (State != "CHECKING" || AccountCheck != "PENDING" || now < NextActionAt) return false;
        if (AwaitingReply)
        {
            AwaitingReply = false; // fence the timed-out attempt even before the next attempt starts
            if (AttemptNumber == 3)
            {
                AccountCheck = "FAILED_DEPENDENCY";
                FailDependency("ACCOUNT_TIMED_OUT");
            }
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
        if (State != "CHECKING" || AccountCheck != "PENDING" || !AwaitingReply || attemptId != AttemptId ||
            attemptNumber != AttemptNumber || decisionId == Guid.Empty || decidedAt < AdmittedAt ||
            decidedAt > receivedAt || decidedAt >= AttemptDeadline || receivedAt >= AttemptDeadline ||
            receivedAt >= OverallDeadline) return false;
        DecisionId = decisionId;
        DecidedAt = decidedAt;
        AwaitingReply = false;
        AccountCheck = allowed ? "SUCCEEDED" : "REJECTED";
        if (allowed)
        {
            NextActionAt = OverallDeadline;
            CheckJoin();
        }
        else Reject("CURRENT_ACCESS_DENIED");
        return true;
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
        SkipPendingChecks();
        State = "FAILED_DEPENDENCY";
        Code = code;
        AwaitingReply = false;
    }
}
