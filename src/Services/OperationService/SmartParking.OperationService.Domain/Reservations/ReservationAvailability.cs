namespace SmartParking.OperationService.Domain.Reservations;

public static class ReservationAvailability
{
    public static AvailabilityDecision Evaluate(CapacityDemand proposed, IReadOnlyList<CapacityDemand> claims,
        IReadOnlyList<CapacityVisit> visits, IReadOnlyList<OperationalCapacityConfig> quotas,
        DateTimeOffset evaluatedAt, TimeSpan occupancyProjection,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations = null, CancellationToken cancellationToken = default)
    {
        if (proposed.Start >= proposed.End) throw new ArgumentException("The effective interval must be nonempty.", nameof(proposed));
        if (proposed.EffectiveSlot is not Guid selected || selected == Guid.Empty)
            throw new ArgumentException("Availability requires a selected slot.", nameof(proposed));

        var siteLimits = quotas.Where(q => q.SpatialUnitId == null).ToArray();
        if (siteLimits.Length != 1 || !siteLimits[0].IsActive || siteLimits[0].MaxReservationQuota < 0 ||
            siteLimits[0].MaxReservationQuota > siteLimits[0].TotalPhysicalCapacity)
            return new("CAPACITY_CONFIGURATION_REQUIRED");

        var bookings = claims.Where(c => c.Id != proposed.Id).Append(proposed).ToArray();
        var horizon = evaluatedAt + occupancyProjection;
        var allocations = bookings.SelectMany(c => c.Allocations ?? []).Concat(visits.SelectMany(v => v.Allocations ?? []))
            .Concat(unresolvedAllocations ?? []);
        // A named commitment can change at an allocation or scan boundary. The quota also changes
        // at booking boundaries, even for other slots at the same site.
        var boundaries = bookings.SelectMany(c => new[] { c.Start, c.End })
            .Concat(allocations.SelectMany(a => new[] { a.Start, a.End, a.ScannedAt ?? a.Start }))
            .Concat(visits.Select(v => v.Entry)).Append(horizon)
            .Where(at => proposed.Start <= at && at < proposed.End).Distinct().Order();

        foreach (var at in boundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = EvaluateAt(selected, proposed.Id, at, horizon, bookings, visits, unresolvedAllocations,
                siteLimits[0].MaxReservationQuota);
            if (!decision.Allowed) return decision;
        }
        return new(null);
    }

    private static AvailabilityDecision EvaluateAt(Guid selected, Guid proposedId, DateTimeOffset at, DateTimeOffset horizon,
        CapacityDemand[] bookings, IReadOnlyList<CapacityVisit> visits,
        IReadOnlyList<CapacityAllocation>? unresolvedAllocations, int quota)
    {
        var observations = visits.Where(v => v.Entry <= at)
            .Select(v => v.LocateAt(at, horizon)).Where(v => v.HasValue).Select(v => v.GetValueOrDefault()).ToArray();
        var linked = observations.ToLookup(v => v.Visit.ReservationId);
        var linkedClaims = new HashSet<Guid>();
        var needsReconciliation = observations.Count(v => v.Slot == selected) > 1 ||
            observations.Any(v => v.Inconsistent && v.Pins.Contains(selected)) ||
            unresolvedAllocations?.Any(a => a.Slot == selected && a.ActiveAt(at)) == true;
        var quotaUsage = 0;
        foreach (var booking in bookings)
        {
            var live = booking.Start <= at && at < booking.End;
            if (live && booking.EffectiveSlot.HasValue && booking.CountsQuota) quotaUsage++;

            var effective = (booking.Allocations ?? []).Where(a => a.ActiveAt(at)).ToArray();
            var original = live ? booking.EffectiveSlot : null;
            if (original is null && effective.Length == 0) continue;

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
                needsReconciliation |= original == selected || pins.Contains(selected) ||
                    attached.Any(v => v.Pins.Contains(selected));
            }
            else if (booking.Id != proposedId && (slot ?? observed.Slot) == selected)
                return new("SLOT_TIME_CONFLICT");
        }

        if (quotaUsage > quota) return new("RESERVATION_QUOTA_EXCEEDED");
        if (needsReconciliation) return new("OCCUPANCY_RECONCILIATION_REQUIRED");
        if (observations.Any(v => v.Slot == selected &&
            (v.Visit.ReservationId is not Guid owner || !linkedClaims.Contains(owner))))
            return new("CAPACITY_UNAVAILABLE");
        return new(null);
    }
}
