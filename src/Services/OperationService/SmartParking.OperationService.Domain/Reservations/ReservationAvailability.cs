using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Domain.Reservations;

public sealed class OperationalCapacityConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? SpatialUnitId { get; set; }
    public string VehicleType { get; set; } = "";
    public int TotalPhysicalCapacity { get; set; }
    public int MaxReservationQuota { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// Evidence owned by Operations, not an audit log. Future cancellation handlers must persist it
// in the cancellation transaction under the same account guard as new-booking admission.
public sealed class ReservationCancellationEvidence
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTimeOffset CancelledAt { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset? RestrictedUntil { get; private set; }
    public string PolicySnapshot { get; private set; } = "";
    private ReservationCancellationEvidence() { }
    public static ReservationCancellationEvidence Record(Guid id, Guid reservation, Guid account,
        DateTimeOffset at, string reason, DateTimeOffset? restrictedUntil, string policySnapshot)
    {
        if (id == Guid.Empty || reservation == Guid.Empty || account == Guid.Empty ||
            reason is not ("VOLUNTARY" or "FACILITY_FAILURE" or "ADMIN_CORRECTION") ||
            restrictedUntil <= at || string.IsNullOrWhiteSpace(policySnapshot) ||
            reason != "VOLUNTARY" && restrictedUntil is not null)
            throw new ArgumentException("Invalid cancellation evidence.");
        return new() { Id = id, ReservationId = reservation, AccountId = account, CancelledAt = at,
            Reason = reason, RestrictedUntil = restrictedUntil, PolicySnapshot = policySnapshot };
    }
}

public sealed record CapacityResource(Guid Id, Guid? Unit, IReadOnlyList<Guid> Ancestors, bool Usable, bool Backup,
    IReadOnlyList<string>? CompatibleCategories = null);
public sealed record CapacityBackup(Guid? Unit, int Count);
public sealed record CapacityDemand(Guid Id, Guid? Slot, Guid? Unit, DateTimeOffset Start, DateTimeOffset End,
    bool CountsQuota = true, string Category = "CAR");
public sealed record CapacityVisit(Guid Id, Guid? ReservationId, Guid? Slot, DateTimeOffset Entry, string Category = "CAR");
public sealed record AvailabilityDecision(string? Rejection)
{
    public bool Allowed => Rejection is null;
}

public static class ReservationAvailability
{
    public static bool InScope(CapacityResource resource, Guid? unit) =>
        unit is null || resource.Unit == unit || resource.Ancestors.Contains(unit.Value);
    private static bool Supports(CapacityResource resource, string category) =>
        resource.CompatibleCategories?.Contains(category) ?? category == "CAR";

    public static AvailabilityDecision Evaluate(CapacityDemand proposed, IReadOnlyList<CapacityDemand> claims,
        IReadOnlyList<CapacityVisit> visits, IReadOnlyList<CapacityResource> resources,
        IReadOnlyList<CapacityBackup> backups, IReadOnlyList<OperationalCapacityConfig> quotas,
        DateTimeOffset evaluatedAt, TimeSpan occupancyProjection, bool mayUseBackup)
    {
        if (resources.Select(x => x.Id).Distinct().Count() != resources.Count ||
            proposed.Slot is null || !resources.Any(r => r.Id == proposed.Slot && r.Usable && Supports(r, proposed.Category)))
            return new("SLOT_UNAVAILABLE");
        var others = claims.Where(c => c.Id != proposed.Id).ToArray();
        if (others.Any(c => c.Slot == proposed.Slot && c.Start < proposed.End && proposed.Start < c.End))
            return new("SLOT_TIME_CONFLICT");
        var siteQuotas = quotas.Where(q => q.SpatialUnitId is null).ToArray();
        if (siteQuotas.Length != 1 || quotas.GroupBy(q => q.SpatialUnitId).Any(g => g.Count() != 1) ||
            quotas.Any(q => !q.IsActive || q.MaxReservationQuota < 0 ||
                q.MaxReservationQuota > q.TotalPhysicalCapacity))
            return new("CAPACITY_CONFIGURATION_REQUIRED");

        var occupiedUntil = evaluatedAt + occupancyProjection;
        var boundaries = others.SelectMany(c => new[] { c.Start, c.End })
            .Append(proposed.Start).Append(occupiedUntil)
            .Where(t => t >= proposed.Start && t < proposed.End).Distinct().Order().ToArray();
        foreach (var at in boundaries)
        {
            var demands = others.Where(c => c.Start <= at && at < c.End).Append(proposed).ToArray();
            foreach (var quota in quotas)
            {
                var count = demands.Count(d => d.CountsQuota && (quota.SpatialUnitId is null || d.Unit == quota.SpatialUnitId ||
                    resources.Any(r => (r.Id == d.Slot || d.Slot is null && r.Unit == d.Unit) && InScope(r, quota.SpatialUnitId))));
                if (count > quota.MaxReservationQuota) return new("RESERVATION_QUOTA_EXCEEDED");
            }
            // A visit and its booking are one vehicle use. A different occupant remains a
            // separate demand; it never fulfills the claimant merely by occupying its slot.
            var activeVisits = at < occupiedUntil ? visits.Where(v => v.Entry <= at).ToArray() : [];
            var occupiedIds = activeVisits.Where(v => v.Slot is not null).Select(v => v.Slot!.Value).ToHashSet();
            if (activeVisits.Where(v => v.Slot is not null).GroupBy(v => v.Slot).Any(g => g.Count() > 1))
                return new("OCCUPANCY_RECONCILIATION_REQUIRED");
            var linked = activeVisits.Where(v => v.ReservationId is not null).Select(v => v.ReservationId!.Value).ToHashSet();
            var available = resources.Where(r => r.Usable && !occupiedIds.Contains(r.Id)).ToArray();
            var candidates = new List<Guid[]>();
            foreach (var demand in demands.Where(d => !linked.Contains(d.Id)))
                candidates.Add(available.Where(r => Supports(r, demand.Category) && InScope(r, demand.Unit) &&
                    (demand.Slot is null || r.Id == demand.Slot)).Select(r => r.Id).ToArray());
            foreach (var visit in activeVisits.Where(v => v.Slot is null))
                candidates.Add(available.Where(r => Supports(r, visit.Category)).Select(r => r.Id).ToArray());
            if (!mayUseBackup)
            {
                // Fixed backup overlapping occupancy is removed only once.
                foreach (var resource in available.Where(r => r.Backup)) candidates.Add([resource.Id]);
                foreach (var backup in backups)
                {
                    var scope = resources.Where(r => InScope(r, backup.Unit)).ToArray();
                    var extra = Math.Max(0, backup.Count - scope.Count(r => r.Backup));
                    for (var i = 0; i < extra; i++)
                        candidates.Add(available.Where(r => InScope(r, backup.Unit) && !r.Backup).Select(r => r.Id).ToArray());
                }
            }
            // Match identities, rather than subtracting overlapping counters. Narrow scopes
            // can use only their own resources; each resource satisfies at most one demand.
            var assigned = new Dictionary<Guid, int>();
            bool Assign(int index, HashSet<Guid> seen)
            {
                foreach (var id in candidates[index])
                {
                    if (!seen.Add(id)) continue;
                    if (!assigned.TryGetValue(id, out var old) || Assign(old, seen))
                    { assigned[id] = index; return true; }
                }
                return false;
            }
            foreach (var index in Enumerable.Range(0, candidates.Count).OrderBy(i => candidates[i].Length))
                if (!Assign(index, [])) return new("CAPACITY_UNAVAILABLE");
        }
        return new(null);
    }
}
