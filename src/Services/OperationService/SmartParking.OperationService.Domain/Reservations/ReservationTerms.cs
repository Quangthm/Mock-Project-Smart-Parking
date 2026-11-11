namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>Immutable owner-issued terms, stored for review and bound to one booking workflow.</summary>
public sealed class ReservationTerms
{
    private ReservationTerms() { }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string IntentFingerprint { get; private set; } = "";
    public string Version { get; private set; } = "";
    public DateTimeOffset ValidUntil { get; private set; }
    public string SnapshotJson { get; private set; } = "";
    public Guid? AcceptedRequestId { get; private set; }

    public static ReservationTerms Issue(Guid id, Guid accountId, string fingerprint, string version,
        DateTimeOffset validUntil, string snapshotJson) => new()
    {
        Id = id, AccountId = accountId, IntentFingerprint = fingerprint, Version = version,
        ValidUntil = validUntil, SnapshotJson = snapshotJson
    };

    public bool CanAccept(Guid accountId, string fingerprint, string version, Guid requestId, DateTimeOffset now) =>
        AccountId == accountId && IntentFingerprint == fingerprint && Version == version && now < ValidUntil &&
        (AcceptedRequestId is null || AcceptedRequestId == requestId);

    public void Accept(Guid requestId)
    {
        if (AcceptedRequestId is not null && AcceptedRequestId != requestId)
            throw new InvalidOperationException("Reviewed terms already belong to another workflow.");
        AcceptedRequestId = requestId;
    }
}
