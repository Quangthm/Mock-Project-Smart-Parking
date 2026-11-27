namespace SmartParking.OperationService.Domain.Reservations;

public static class ReservationAvailability
{
    public static AvailabilityDecision Evaluate(CapacityDemand proposed, IReadOnlyList<CapacityDemand> claims,
        IReadOnlyList<CapacityVisit> visits, int? maxReservationQuota,
        DateTimeOffset evaluatedAt, TimeSpan occupancyProjection,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations = null, CancellationToken cancellationToken = default)
    {
        if (proposed.Start >= proposed.End) throw new ArgumentException("The effective interval must be nonempty.", nameof(proposed));
        if (proposed.EffectiveSlot is not Guid selected || selected == Guid.Empty)
            throw new ArgumentException("Availability requires a selected slot.", nameof(proposed));

        if (maxReservationQuota is < 0) return new("RESERVATION_QUOTA_CONFIGURATION_INVALID");

        var bookings = claims.Where(c => c.Id != proposed.Id).Append(proposed).ToArray();
        var horizon = evaluatedAt + occupancyProjection;
        var boundaries = Boundaries(proposed.Start, proposed.End, horizon, bookings, visits, unresolvedAllocations);
        foreach (var at in boundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = ObserveAt(proposed.Id, at, horizon, bookings, visits, unresolvedAllocations, cancellationToken);
            var decision = state.ForSlot(selected, maxReservationQuota);
            if (!decision.Allowed) return decision;
        }
        return new(null);
    }

    public static AvailabilityDecision EvaluatePhysicalOccupancy(Guid slotId, DateTimeOffset at,
        IReadOnlyList<CapacityDemand> commitments, IReadOnlyList<CapacityVisit> visits,
        IReadOnlyList<CapacityAllocation> unresolvedAllocations) =>
        ObserveAt(null, at, null, commitments, visits, unresolvedAllocations, default).ForSlot(slotId, null);

    // The caller supplies the complete set of eligible, registered CAR slot identities.
    // A null interval is current physical mode and deliberately ignores both quota and projection.
    public static int? CountAvailable(IReadOnlyCollection<Guid> candidateSlots, ReservationInterval? effectiveInterval,
        DateTimeOffset evaluatedAt, TimeSpan projection, IReadOnlyList<CapacityDemand> commitments,
        IReadOnlyList<CapacityVisit> visits, IReadOnlyList<CapacityAllocation> unresolved, int? quota,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var candidates = candidateSlots.ToHashSet();
        if (candidates.Contains(Guid.Empty) || candidates.Count != candidateSlots.Count)
            throw new ArgumentException("Candidate slots must have distinct, nonempty identities.", nameof(candidateSlots));
        var free = new HashSet<Guid>(candidates);
        DateTimeOffset? horizon = effectiveInterval is null ? null : evaluatedAt + projection;
        if (effectiveInterval is not null && quota < 0) return null;
        var remainingQuota = int.MaxValue;
        IEnumerable<DateTimeOffset> boundaries = effectiveInterval is null ? [evaluatedAt] :
            Boundaries(effectiveInterval.Start, effectiveInterval.End, horizon!.Value, commitments, visits, unresolved);
        foreach (var at in boundaries)
        {
            ct.ThrowIfCancellationRequested();
            var state = ObserveAt(null, at, horizon, commitments, visits, unresolved, ct);
            if (state.UnlocatedCommitment || candidates.Overlaps(state.Uncertain)) return null;
            free.ExceptWith(state.Claimed);
            free.ExceptWith(state.Occupied);
            if (effectiveInterval is not null && quota is int limit)
                remainingQuota = Math.Min(remainingQuota, Math.Max(0, limit - state.QuotaUsage));
        }
        return Math.Min(free.Count, remainingQuota);
    }

    private static IEnumerable<DateTimeOffset> Boundaries(DateTimeOffset start, DateTimeOffset end, DateTimeOffset horizon,
        IReadOnlyList<CapacityDemand> bookings, IReadOnlyList<CapacityVisit> visits,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations)
    {
        var allocations = bookings.SelectMany(c => c.Allocations ?? []).Concat(visits.SelectMany(v => v.Allocations ?? []))
            .Concat(unresolvedAllocations ?? []);
        // A named commitment can change at an allocation or scan boundary. The quota also changes
        // at booking boundaries, even for other slots at the same site.
        return bookings.SelectMany(c => new[] { c.Start, c.End })
            .Concat(allocations.SelectMany(a => new[] { a.Start, a.End, a.ScannedAt ?? a.Start }))
            .Concat(visits.Select(v => v.Entry)).Append(horizon).Append(start)
            .Where(at => start <= at && at < end).Distinct().Order();
    }

    private static SlotAvailabilityAtInstant ObserveAt(Guid? proposedId, DateTimeOffset at, DateTimeOffset? horizon,
        IReadOnlyList<CapacityDemand> bookings, IReadOnlyList<CapacityVisit> visits,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations, CancellationToken ct)
    {
        var observations = visits.Where(v => horizon is null || v.Entry <= at)
            .Select(v => v.LocateAt(at, horizon)).Where(v => v.HasValue).Select(v => v.GetValueOrDefault()).ToArray();
        var linked = observations.ToLookup(v => v.Visit.ReservationId);
        var linkedClaims = new HashSet<Guid>();
        var state = new SlotAvailabilityAtInstant();
        foreach (var group in observations.Where(v => v.Slot.HasValue).GroupBy(v => v.Slot!.Value))
            if (group.Skip(1).Any()) state.Uncertain.Add(group.Key);
        foreach (var observation in observations.Where(v => v.Inconsistent))
            state.Uncertain.UnionWith(observation.Pins);
        foreach (var allocation in unresolvedAllocations ?? [])
            if (allocation.ActiveAt(at)) state.Uncertain.Add(allocation.Slot);

        foreach (var booking in bookings)
        {
            ct.ThrowIfCancellationRequested();
            var live = booking.Start <= at && at < booking.End;
            if (live && booking.EffectiveSlot.HasValue && booking.CountsQuota) state.QuotaUsage++;

            var effective = (booking.Allocations ?? []).Where(a => a.ActiveAt(at)).ToArray();
            var original = live ? booking.EffectiveSlot : null;
            if (original is null && effective.Length == 0)
            {
                // An older live, unassigned CAR commitment cannot establish an exact site count.
                if (live && booking.CountsQuota) state.UnlocatedCommitment = true;
                continue;
            }

            var attached = linked[booking.Id].ToArray();
            var observed = attached.FirstOrDefault();
            var pins = effective.Select(a => a.Slot).Distinct().ToArray();
            var slot = pins.Length == 1 ? pins[0] : original;
            // A different allocation cannot release the current claim without a corroborating scan.
            // An explicitly changed claim must agree with its allocation.
            var observedReplacement = effective.Any(a => a.ObservedAssignment(booking.Id, at) &&
                attached.Length == 1 && observed.Visit.Id == a.SessionId && observed.Slot == a.Slot);
            var conflictingClaim = original.HasValue && slot != original &&
                (!observedReplacement || booking.Slot.HasValue && booking.TargetSlot != booking.Slot);
            var inconsistent = pins.Length > 1 || conflictingClaim ||
                effective.Any(a => a.ReservationId.HasValue && a.ReservationId != booking.Id) ||
                attached.Length > 1 || attached.Length == 1 && (observed.Inconsistent ||
                observed.Visit.Category != booking.Category || observed.Slot.HasValue && slot.HasValue && observed.Slot != slot);

            linkedClaims.Add(booking.Id);
            if (inconsistent)
            {
                if (original is Guid originalSlot) state.Uncertain.Add(originalSlot);
                state.Uncertain.UnionWith(pins);
                foreach (var observation in attached) state.Uncertain.UnionWith(observation.Pins);
            }
            else if (booking.Id != proposedId && (slot ?? observed.Slot) is Guid claimed)
                state.Claimed.Add(claimed);
        }

        foreach (var observation in observations)
            if (observation.Slot is Guid occupied &&
                (observation.Visit.ReservationId is not Guid owner || !linkedClaims.Contains(owner)))
                state.Occupied.Add(occupied);
        return state;
    }
}
