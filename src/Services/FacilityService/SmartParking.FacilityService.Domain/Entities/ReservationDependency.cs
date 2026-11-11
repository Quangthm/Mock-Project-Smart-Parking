namespace SmartParking.FacilityService.Domain.Entities;

// The token is retained after resolution: absence must never resurrect a delayed acquire.
public sealed class ReservationDependency
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid SlotId { get; private set; }
    public string Purpose { get; private set; } = "";
    public string IntentFingerprint { get; private set; } = "";
    public DateTimeOffset Deadline { get; private set; }
    public string State { get; private set; } = "";
    public string? SnapshotJson { get; private set; }
    public string? OutcomeFingerprint { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private ReservationDependency() { }

    public static ReservationDependency Create(Guid id, Guid workflow, Guid tenant, Guid site,
        Guid slot, string purpose, string fingerprint, DateTimeOffset deadline, DateTimeOffset now) =>
        new() { Id = id, WorkflowId = workflow, TenantId = tenant, SiteId = site, SlotId = slot,
            Purpose = purpose, IntentFingerprint = fingerprint, Deadline = deadline,
            State = "PREPARING", CreatedAt = now, UpdatedAt = now };

    public void Hold(string snapshot, DateTimeOffset now)
    {
        if (State != "PREPARING" || now >= Deadline)
            throw new StructureException("STRUCTURE_CONFLICT", "The dependency cannot be acquired.");
        SnapshotJson = snapshot; State = "HELD"; UpdatedAt = now;
    }
    public void Abort(DateTimeOffset now)
    {
        if (State == "COMPLETED") throw new StructureException("STRUCTURE_CONFLICT", "Committed dependency cannot be aborted.");
        State = "ABORTED"; UpdatedAt = now;
    }
    public void Complete(string fingerprint, DateTimeOffset now)
    {
        if (State == "COMPLETED" && OutcomeFingerprint == fingerprint) return;
        if (State != "HELD") throw new StructureException("STRUCTURE_CONFLICT", "Dependency outcome conflicts with its recorded state.");
        State = "COMPLETED"; OutcomeFingerprint = fingerprint; UpdatedAt = now;
    }
}
