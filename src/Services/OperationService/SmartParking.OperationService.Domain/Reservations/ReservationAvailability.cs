using Choice = (System.Guid Owner, System.DateTimeOffset Since);
using Constraint = (System.Guid? Booking, (System.Guid Owner, System.DateTimeOffset Since)? Choice,
    System.Guid? Pinned, System.Guid[] Scope, System.Guid[] Options, string? Error);

namespace SmartParking.OperationService.Domain.Reservations;

public static class ReservationAvailability
{
    public static bool InScope(CapacityResource resource, Guid? unit) =>
        unit is null || resource.Unit == unit || resource.Ancestors.Contains(unit.Value);

    private static bool Supports(CapacityResource resource, string category) =>
        resource.CompatibleCategories?.Contains(category) ?? category == "CAR";

    public static AvailabilityDecision Evaluate(CapacityDemand proposed, IReadOnlyList<CapacityDemand> claims,
        IReadOnlyList<CapacityVisit> visits, IReadOnlyList<CapacityResource> resources,
        IReadOnlyList<CapacityBackup> backups, IReadOnlyList<OperationalCapacityConfig> quotas,
        DateTimeOffset evaluatedAt, TimeSpan occupancyProjection, bool hasScopedCapacityEvidence,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations = null, CancellationToken cancellationToken = default)
    {
        if (proposed.Start >= proposed.End) throw new ArgumentException("The effective interval must be nonempty.", nameof(proposed));
        if (resources.Select(r => r.Id).Distinct().Count() != resources.Count)
            return new("CAPACITY_CONFIGURATION_REQUIRED");
        var selected = proposed.Slot ?? proposed.TargetSlot;
        if (selected.HasValue && !resources.Any(r => r.Id == selected && r.Usable && !r.Backup &&
                Supports(r, proposed.Category) && !(r.Exclusions ?? []).Any(x => x.Start < proposed.End && proposed.Start < x.End)))
            return new("SLOT_UNAVAILABLE");

        var siteLimits = quotas.Where(q => q.SpatialUnitId == null).ToArray();
        if (siteLimits.Length != 1 || !siteLimits[0].IsActive || siteLimits[0].MaxReservationQuota < 0 ||
            siteLimits[0].MaxReservationQuota > siteLimits[0].TotalPhysicalCapacity)
            return new("CAPACITY_CONFIGURATION_REQUIRED");

        var bookings = claims.Where(c => c.Id != proposed.Id).Append(proposed).ToArray();
        var bySlot = resources.ToDictionary(r => r.Id);
        var flexible = bookings.Where(c => c.Slot == null && c.TargetSlot == null).ToArray();
        var lower = flexible.Select(c => c.Start).Append(proposed.Start).Min();
        var upper = flexible.Select(c => c.End).Append(proposed.End).Max();
        var horizon = evaluatedAt + occupancyProjection;
        var allocations = bookings.SelectMany(c => c.Allocations ?? []).Concat(visits.SelectMany(v => v.Allocations ?? []))
            .Concat(unresolvedAllocations ?? []);
        var boundaries = bookings.SelectMany(c => new[] { c.Start, c.End })
            .Concat(allocations.SelectMany(a => new[] { a.Start, a.End, a.ScannedAt ?? a.Start }))
            .Concat(resources.SelectMany(r => r.Exclusions ?? []).SelectMany(x => new[] { x.Start, x.End }))
            .Concat(visits.Select(v => v.Entry)).Append(horizon)
            .Where(t => lower <= t && t < upper).Distinct().Order();
        var slices = new List<List<Constraint>>();
        foreach (var at in boundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = BuildSlice(at, horizon, bookings, visits, resources, bySlot, backups, hasScopedCapacityEvidence);
            foreach (var orphan in unresolvedAllocations ?? [])
                if (orphan.ActiveAt(at))
                    slice.Add((null, null, null, [orphan.Slot], [], "OCCUPANCY_RECONCILIATION_REQUIRED"));
            if (proposed.Start <= at && at < proposed.End)
            {
                if (selected.HasValue && slice.Any(n => n.Booking.HasValue && n.Booking != proposed.Id &&
                    n.Pinned == selected && n.Error == null)) return new("SLOT_TIME_CONFLICT");
                if (bookings.Count(b => b.CountsQuota && b.Start <= at && at < b.End) > siteLimits[0].MaxReservationQuota)
                    return new("RESERVATION_QUOTA_EXCEEDED");
            }
            if (selected.HasValue && flexible.Length == 0 && slice.All(n => n.Error == null && n.Pinned.HasValue))
            {
                // No choice links this fixed-only slice to a different time. Resolve it without storing a graph.
                if (slice.Count(n => n.Pinned == selected) > 1) return new("CAPACITY_UNAVAILABLE");
                continue;
            }
            slices.Add(slice);
        }
        if (slices.Count == 0) return new(null);
        return Solve(AffectedComponent(slices, proposed.Id), cancellationToken);
    }

    private static List<Constraint> BuildSlice(DateTimeOffset at, DateTimeOffset horizon, CapacityDemand[] bookings,
        IReadOnlyList<CapacityVisit> visits, IReadOnlyList<CapacityResource> resources,
        IReadOnlyDictionary<Guid, CapacityResource> bySlot, IReadOnlyList<CapacityBackup> backups, bool evidence)
    {
        var nodes = new List<Constraint>();
        var linked = new Dictionary<Guid, List<CapacityVisit>>();
        var occupied = new HashSet<Guid>();
        foreach (var visit in visits.Where(v => v.Entry <= at))
        {
            var active = (visit.Allocations ?? []).Where(a => a.ActiveAt(at)).ToArray();
            // An observed walk-in occupies through the projection horizon. Its session-only scan row
            // (including the old open-end sentinel) has no independent reservation interval.
            var assigned = active.Where(a => a.ReservationId.HasValue).ToArray();
            if (at >= horizon && assigned.Length == 0) continue;
            var pins = active.Select(a => a.Slot).Append(visit.Slot ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
            var invalid = pins.Length > 1 || active.Any(a => a.SessionId != visit.Id || a.ReservationId != visit.ReservationId ||
                !a.ReservationId.HasValue && (a.Status != "OCCUPIED" || visit.Slot != a.Slot));
            var located = visit with { Slot = pins.Length == 1 ? pins[0] : visit.Slot };
            if (invalid || located.Slot is Guid physical && !occupied.Add(physical))
            {
                var scope = pins.Length > 0 ? pins : resources.Select(r => r.Id).ToArray();
                nodes.Add((null, null, null, scope, [], "OCCUPANCY_RECONCILIATION_REQUIRED"));
            }
            if (visit.ReservationId is Guid id)
            {
                if (!linked.TryGetValue(id, out var attached)) linked[id] = attached = [];
                attached.Add(located);
            }
            else nodes.Add(VisitConstraint(located, at, resources));
        }

        foreach (var booking in bookings)
        {
            var live = booking.Start <= at && at < booking.End;
            var allocations = booking.Allocations ?? [];
            var effective = allocations.Where(a => a.ActiveAt(at)).ToArray();
            if (!live && effective.Length == 0) continue;
            linked.Remove(booking.Id, out var attached);
            var visit = attached?.FirstOrDefault();
            var original = live ? booking.Slot ?? booking.TargetSlot : null;
            var pins = effective.Select(a => a.Slot).Distinct().ToArray();
            var slot = pins.Length == 1 ? pins[0] : original;
            string? error = null;
            var observedReplacement = effective.Any(a => a.ObservedAssignment(booking.Id, at) &&
                visit != null && visit.Id == a.SessionId && visit.Slot == a.Slot);
            var conflictingClaim = original.HasValue && slot != original &&
                (!observedReplacement || booking.Slot.HasValue && booking.TargetSlot != booking.Slot);
            if (pins.Length > 1 || conflictingClaim || effective.Any(a => a.ReservationId.HasValue && a.ReservationId != booking.Id) ||
                attached?.Count > 1 || visit != null && (visit.Category != booking.Category ||
                visit.Slot.HasValue && slot.HasValue && visit.Slot != slot))
                error = "OCCUPANCY_RECONCILIATION_REQUIRED";
            slot ??= visit?.Slot;

            // Ordinary partial pins constrain the same booking choice on both sides of their interval.
            // A corroborated physical reassignment is the only cut in that continuity.
            Choice? choice = null;
            if (live && original == null)
            {
                // The first scan alone proves a location, not a switch from an earlier slot.
                var lastTransition = allocations.Where(a => a.ObservedAssignment(booking.Id, at) && a.Start <= at &&
                        allocations.Any(prior => prior.Slot != a.Slot && prior.End <= a.Start &&
                            prior.ObservedAssignment(booking.Id, prior.End)))
                    .Select(a => Max(a.Start, a.ScannedAt!.Value)).Append(booking.Start).Max();
                choice = (booking.Id, lastTransition);
            }
            IEnumerable<CapacityResource> supply = resources;
            if (slot is Guid pinned) supply = bySlot.TryGetValue(pinned, out var resource) ? [resource] : [];
            var scope = supply.Where(r => Supports(r, booking.Category) &&
                (!evidence || InScope(r, booking.Unit))).ToArray();
            var footprint = slot.HasValue ? new[] { slot.Value } : scope.Select(r => r.Id).ToArray();
            if (error != null)
                footprint = footprint.Concat(pins).Concat(new[] { original, visit?.Slot }.OfType<Guid>()).Distinct().ToArray();
            if (live && original == null && effective.Length == 0 && (!evidence || slot.HasValue && scope.Length == 0))
            {
                error ??= evidence ? "OCCUPANCY_RECONCILIATION_REQUIRED" : "SCOPED_CAPACITY_EVIDENCE_REQUIRED";
                // A linked observation cannot certify which slots can fulfill an unassigned scope.
                footprint = resources.Where(r => Supports(r, booking.Category) && (!evidence || InScope(r, booking.Unit)))
                    .Select(r => r.Id).Union(footprint).ToArray();
            }
            var options = scope.Where(r => (!r.Backup || slot == r.Id) &&
                AvailableAt(r, at)).Select(r => r.Id).ToArray();
            nodes.Add((booking.Id, choice, slot, footprint, options, error));
        }

        foreach (var remaining in linked.Values)
            foreach (var visit in remaining) nodes.Add(VisitConstraint(visit, at, resources));
        foreach (var backup in backups)
        {
            var scope = resources.Where(r => InScope(r, backup.Unit)).ToArray();
            var extra = backup.Count - scope.Count(r => r.Backup);
            if (extra <= 0) continue;
            var ordinary = scope.Where(r => !r.Backup).ToArray();
            var ids = ordinary.Select(r => r.Id).ToArray();
            var options = ordinary.Where(r => AvailableAt(r, at)).Select(r => r.Id).ToArray();
            for (var i = 0; i < Math.Min(extra, options.Length + 1); i++)
                nodes.Add((null, null, null, ids, options, null));
        }
        return nodes;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static bool AvailableAt(CapacityResource r, DateTimeOffset at) =>
        r.Usable && !(r.Exclusions ?? []).Any(x => x.Start <= at && at < x.End);

    private static Constraint VisitConstraint(CapacityVisit visit, DateTimeOffset at, IReadOnlyList<CapacityResource> resources)
    {
        if (visit.Slot is Guid slot) return (null, null, slot, [slot], [slot], null);
        var compatible = resources.Where(r => !r.Backup && Supports(r, visit.Category)).ToArray();
        return (null, null, null, compatible.Select(r => r.Id).ToArray(),
            compatible.Where(r => AvailableAt(r, at)).Select(r => r.Id).ToArray(), null);
    }

    // Only simultaneous competitors and the same continuous choice in another slice can affect this request.
    private static List<Constraint[]> AffectedComponent(List<List<Constraint>> slices, Guid proposed)
    {
        var pending = new Queue<(int Slice, int Node)>();
        var seen = new HashSet<(int Slice, int Node)>();
        var choices = new Dictionary<Choice, List<(int, int)>>();
        var owners = new Dictionary<(int, Guid), List<(int, int)>>();
        for (var s = 0; s < slices.Count; s++)
            for (var i = 0; i < slices[s].Count; i++)
            {
                var node = slices[s][i];
                if (node.Booking == proposed) pending.Enqueue((s, i));
                if (node.Choice is Choice choice)
                {
                    if (!choices.TryGetValue(choice, out var group)) choices[choice] = group = [];
                    group.Add((s, i));
                }
                foreach (var slot in node.Scope)
                {
                    if (!owners.TryGetValue((s, slot), out var group)) owners[(s, slot)] = group = [];
                    group.Add((s, i));
                }
            }
        while (pending.TryDequeue(out var index))
        {
            if (!seen.Add(index)) continue;
            var node = slices[index.Slice][index.Node];
            if (node.Choice is Choice choice && choices.Remove(choice, out var group))
                foreach (var related in group) pending.Enqueue(related);
            foreach (var slot in node.Scope)
                if (owners.Remove((index.Slice, slot), out var peers))
                    foreach (var related in peers) pending.Enqueue(related);
        }
        return seen.GroupBy(x => x.Slice).Select(g => g.Select(i => slices[i.Slice][i.Node]).ToArray()).ToList();
    }

    private static AvailabilityDecision Solve(List<Constraint[]> slices, CancellationToken cancellationToken)
    {
        var error = slices.SelectMany(s => s).Select(n => n.Error).FirstOrDefault(e => e != null);
        if (error != null) return new(error);
        static Guid[] ContinuousOptions(IEnumerable<Constraint> parts)
        {
            var options = parts.First().Options.ToHashSet();
            foreach (var part in parts) options.IntersectWith(part.Options);
            return options.ToArray();
        }
        var continuous = slices.SelectMany(s => s).Where(n => n.Choice.HasValue).ToLookup(n => n.Choice!.Value);
        if (continuous.Any(g => g.Select(n => n.Pinned).OfType<Guid>().Distinct().Skip(1).Any()))
            return new("OCCUPANCY_RECONCILIATION_REQUIRED");
        var variables = continuous.Select(g => (Key: g.Key, Slots: ContinuousOptions(g)))
            .OrderBy(x => x.Slots.Length).ToArray();
        var assignment = new Dictionary<Choice, Guid>();
        bool Fits() => slices.All(slice => CanAssignDistinctResources(slice.Select(n =>
            n.Choice is Choice key && assignment.TryGetValue(key, out var slot) ? new[] { slot } : n.Options).ToList()));
        if (!Fits()) return new("CAPACITY_UNAVAILABLE");
        // Explore all whole-interval choices. Slice matching only prunes a branch, never changes a chosen slot.
        var nextOption = new int[variables.Length];
        var depth = 0;
        while (depth >= 0 && depth < variables.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (key, slots) = variables[depth];
            if (nextOption[depth] == slots.Length)
            {
                assignment.Remove(key);
                nextOption[depth--] = 0;
                continue;
            }
            assignment[key] = slots[nextOption[depth]++];
            if (Fits()) depth++;
        }
        return new(depth == variables.Length ? null : "CAPACITY_UNAVAILABLE");
    }

    private static bool CanAssignDistinctResources(List<Guid[]> candidates)
    {
        var assigned = new Dictionary<Guid, int>();
        bool Assign(int index, HashSet<Guid> seen)
        {
            foreach (var id in candidates[index])
            {
                if (!seen.Add(id)) continue;
                if (assigned.TryGetValue(id, out var old) && !Assign(old, seen)) continue;
                assigned[id] = index;
                return true;
            }
            return false;
        }
        return Enumerable.Range(0, candidates.Count).OrderBy(i => candidates[i].Length).All(i => Assign(i, []));
    }
}
