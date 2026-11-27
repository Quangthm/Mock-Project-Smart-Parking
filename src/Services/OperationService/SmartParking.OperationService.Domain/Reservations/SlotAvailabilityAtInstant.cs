namespace SmartParking.OperationService.Domain.Reservations;

internal sealed class SlotAvailabilityAtInstant
{
    public HashSet<Guid> Claimed { get; } = [];
    public HashSet<Guid> Occupied { get; } = [];
    public HashSet<Guid> Uncertain { get; } = [];
    public int QuotaUsage { get; set; }
    public bool UnlocatedCommitment { get; set; }

    // Preserve the SPARK-273 admission rejection precedence.
    public AvailabilityDecision ForSlot(Guid slot, int? quota)
    {
        if (Claimed.Contains(slot)) return new("SLOT_TIME_CONFLICT");
        if (quota is int limit && QuotaUsage > limit) return new("RESERVATION_QUOTA_EXCEEDED");
        if (Uncertain.Contains(slot)) return new("OCCUPANCY_RECONCILIATION_REQUIRED");
        if (Occupied.Contains(slot)) return new("CAPACITY_UNAVAILABLE");
        return new(null);
    }
}
