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
    public Guid? DecisionId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
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
        string fingerprint, DateTimeOffset now, TimeSpan validity)
    {
        if (userId == Guid.Empty || accountId == Guid.Empty || key == Guid.Empty ||
            string.IsNullOrWhiteSpace(intentJson) || string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("Admission requires identity and normalized intent.");
        var request = new ReservationRequest
        {
            Id = Guid.NewGuid(), UserId = userId, AccountId = accountId, IdempotencyKey = key,
            IntentJson = intentJson, Fingerprint = fingerprint, AdmittedAt = now,
            OverallDeadline = now.Add(validity),
            AccountCheck = "PENDING", PlateCheck = "PENDING", SlotCheck = "PENDING"
        };
        request.AttemptId = Guid.NewGuid();
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

    public void ExpireAdmission()
    {
        if (State == "CHECKING") FailQuoteAdmission("ADMISSION_TIMED_OUT");
        else if (State == "EVALUATING") Reject("BOOKING_PREPARATION_EXPIRED");
    }

    public bool AcceptDecision(Guid attemptId, int attemptNumber, Guid decisionId,
        DateTimeOffset decidedAt, bool allowed, DateTimeOffset receivedAt)
    {
        if (State != "CHECKING" || AccountCheck != "PENDING" || attemptId != AttemptId ||
            attemptNumber != 1 || decisionId == Guid.Empty || decidedAt < AdmittedAt ||
            decidedAt > receivedAt ||
            receivedAt >= OverallDeadline) return false;
        DecisionId = decisionId;
        DecidedAt = decidedAt;
        AccountCheck = allowed ? "SUCCEEDED" : "REJECTED";
        if (allowed) CheckJoin();
        else Reject("CURRENT_ACCESS_DENIED");
        return true;
    }

    private void FailDependency(string code)
    {
        SkipPendingChecks();
        State = "FAILED_DEPENDENCY";
        Code = code;
    }
}
