using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

// Maps a validated Domain aggregate to the message owned by the use case.
// Reservation decides how to apply these physical capacities to its own pools.
public static class StructureOutcomeBuilder
{
    public static StructureOutcome Build(
        ParkingStructure structure, IReadOnlySet<Guid> originalSlots, IReadOnlySet<Guid> originalUnits)
    {
        var capacities = new List<PoolCapacity>();
        foreach (var vehicle in new[] { VehicleType.CAR, VehicleType.MOTORCYCLE })
        {
            var slots = structure.Slots.Where(slot => slot.VehicleType == vehicle).ToArray();
            var siteView = structure.CapacityViews.Single(view => view.UnitId is null && view.VehicleType == vehicle);
            capacities.Add(new PoolCapacity(null, vehicle.ToString(), slots.Length, siteView.EffectiveBackup));

            foreach (var unit in structure.Units)
            {
                var descendants = structure.Units
                    .Where(child => child.Path.StartsWith(unit.Path, StringComparison.Ordinal))
                    .Select(child => child.Id).ToHashSet();
                var view = structure.CapacityViews.Single(view => view.UnitId == unit.Id && view.VehicleType == vehicle);
                capacities.Add(new PoolCapacity(
                    unit.Id, vehicle.ToString(),
                    slots.Count(slot => descendants.Contains(slot.UnitId)), view.EffectiveBackup));
            }
        }

        // Canonical ordering keeps the serialized outcome stable: Reservation compares the
        // persisted receipt with `outcome=$4::jsonb`, and JSONB arrays are order-sensitive.
        return new StructureOutcome(
            structure.Site.IsActive,
            originalSlots.Except(structure.Slots.Select(slot => slot.Id)).OrderBy(id => id).ToArray(),
            originalUnits.Except(structure.Units.Select(unit => unit.Id)).OrderBy(id => id).ToArray(),
            capacities.OrderBy(c => c.UnitId).ThenBy(c => c.VehicleType, StringComparer.Ordinal).ToArray());
    }
}
