namespace SmartParking.ReservationService.Domain;

public sealed record CapacityClaim(Guid? UnitId, string VehicleType, int PendingPayment, int Protected);
public sealed record HoldSnapshot(Guid[] ProtectedSlots, bool HasLiveCommitments, CapacityClaim[]? Capacities = null);
public sealed record PoolCapacity(Guid? UnitId, string VehicleType, int Capacity, int Backup = 0);
public sealed record EditOutcome(bool SiteActive, Guid[] RemovedSlots, Guid[] RemovedUnits, PoolCapacity[] Capacities);
public sealed class HoldConflictException : Exception;
public sealed class InvalidStructureEditException(string message) : Exception(message);

public static class StructureEditPolicy
{
    public static void ValidateScope(Guid tenantId, Guid siteId, Guid token)
    {
        if (tenantId == Guid.Empty || siteId == Guid.Empty || token == Guid.Empty)
            throw new InvalidStructureEditException("Tenant, site and token must be non-empty.");
    }

    public static void ValidateOutcome(EditOutcome? outcome)
    {
        // Null is an abort/read release, never a successfully committed edit.
        if (outcome is null) return;
        if (outcome.RemovedSlots is null || outcome.RemovedUnits is null || outcome.Capacities is null)
            throw new InvalidStructureEditException("All outcome collections are required.");

        if (outcome.RemovedSlots.Concat(outcome.RemovedUnits).Any(id => id == Guid.Empty))
            throw new InvalidStructureEditException("Removed resources must have non-empty identifiers.");

        var pools = new HashSet<(Guid?, string)>();
        foreach (var capacity in outcome.Capacities)
        {
            if (capacity is null || capacity.Capacity < 0 || capacity.Backup < 0 || capacity.Backup > capacity.Capacity ||
                capacity.VehicleType is not ("CAR" or "MOTORCYCLE") || capacity.UnitId == Guid.Empty)
                throw new InvalidStructureEditException("Invalid capacity or backup.");
            if (!pools.Add((capacity.UnitId, capacity.VehicleType)))
                throw new InvalidStructureEditException("Capacity pools must be unique.");
        }
    }
}
