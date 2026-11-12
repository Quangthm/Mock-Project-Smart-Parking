namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>
/// A durable dependency token, not a copy of Facility's resource catalogue.
/// The booking writer locks this row and records READY in the same transaction as its local effect.
/// </summary>
public sealed class ReservationFacilityDependency
{
    private ReservationFacilityDependency() { }

    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string Purpose { get; private set; } = "";
    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid SlotId { get; private set; }
    public string IntentFingerprint { get; private set; } = "";
    public DateTimeOffset Deadline { get; private set; }
    public string State { get; private set; } = "PREPARING";
    public string? SnapshotJson { get; private set; }
    public string? SnapshotFingerprint { get; private set; }
    public string? OutcomeJson { get; private set; }
    public string? AbortReason { get; private set; }
    public string? Resolution { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset NextActionAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static ReservationFacilityDependency Prepare(Guid id, Guid workflowId, string purpose,
        Guid tenantId, Guid siteId, Guid slotId, string fingerprint, DateTimeOffset deadline, DateTimeOffset now)
    {
        if (id == Guid.Empty || workflowId == Guid.Empty || tenantId == Guid.Empty || siteId == Guid.Empty ||
            slotId == Guid.Empty || purpose is not ("ADMISSION" or "CONFIRMATION") ||
            string.IsNullOrWhiteSpace(fingerprint) || deadline <= now)
            throw new ArgumentException("A dependency requires its immutable workflow intent and future deadline.");
        return new()
        {
            Id = id, WorkflowId = workflowId, Purpose = purpose, TenantId = tenantId, SiteId = siteId,
            SlotId = slotId, IntentFingerprint = fingerprint, Deadline = deadline, CreatedAt = now, UpdatedAt = now,
            NextActionAt = now
        };
    }

    public void RecordHeld(string snapshotJson, string snapshotFingerprint, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson) || string.IsNullOrWhiteSpace(snapshotFingerprint))
            throw new ArgumentException("A held dependency requires immutable resource facts.");
        if (SnapshotFingerprint is not null)
        {
            if (State != "HELD") throw new InvalidOperationException("A fenced dependency cannot accept an acquisition replay.");
            // JSONB may normalize textual formatting on reload; the Application layer hashes
            // its canonical snapshot, so compare that persisted semantic identity.
            if (SnapshotFingerprint != snapshotFingerprint)
                throw new InvalidOperationException("Facility replay changed the accepted resource facts.");
            return;
        }
        if (State != "PREPARING" || now >= Deadline)
            throw new InvalidOperationException("A fenced or expired dependency cannot become held.");
        SnapshotJson = snapshotJson;
        SnapshotFingerprint = snapshotFingerprint;
        State = "HELD";
        Changed(now);
        NextActionAt = Deadline;
    }

    public void MarkReady(string outcomeJson, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(outcomeJson)) throw new ArgumentException("A committed local outcome is required.");
        if (State == "READY" && OutcomeJson == outcomeJson) return;
        if (State != "HELD" || now >= Deadline)
            throw new InvalidOperationException("Only a held, unfenced dependency may commit its local outcome.");
        OutcomeJson = outcomeJson;
        State = "READY";
        Changed(now);
    }

    public void BeginAbort(string reason, DateTimeOffset now)
    {
        if (State == "ABORTING" || State == "COMPLETED" && Resolution == "ABORTED") return;
        if (State is not ("PREPARING" or "HELD") || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Committed local work cannot be aborted as an unused preparation.");
        AbortReason = reason;
        State = "ABORTING";
        Changed(now);
    }

    public void Acknowledge(string resolution, DateTimeOffset now)
    {
        if (State == "COMPLETED" && Resolution == resolution) return;
        if (!(State == "READY" && resolution == "COMMITTED" || State == "ABORTING" && resolution == "ABORTED"))
            throw new InvalidOperationException("Acknowledgement must match the fenced local outcome.");
        Resolution = resolution;
        State = "COMPLETED";
        CompletedAt = now;
        Changed(now);
    }

    public void Defer(DateTimeOffset nextActionAt, DateTimeOffset now)
    {
        if (State == "COMPLETED") return;
        if (nextActionAt <= now) throw new ArgumentException("Retry must be scheduled after the observation.");
        Changed(now);
        NextActionAt = nextActionAt;
    }

    private void Changed(DateTimeOffset now)
    {
        Version = checked(Version + 1);
        UpdatedAt = now;
        NextActionAt = now;
    }
}
