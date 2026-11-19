using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ScheduleFacilityDependencyRetryHandler(IReservationFacilityDependencyRepository dependencies,
    IReservationBookingRepository bookings, IUnitOfWork work) : IRequestHandler<ScheduleFacilityDependencyRetryCommand>
{
    public async Task Handle(ScheduleFacilityDependencyRetryCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var row = await dependencies.LockAsync(command.Token, ct);
        if (row is null || row.State == "COMPLETED") return;
        var now = await bookings.DatabaseNowAsync(ct);
        // Existing Operations worker cadence, not a configurable business policy. Advancing
        // this timestamp prevents a batch of unavailable tokens starving later work.
        row.Defer(now.AddSeconds(1), now);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

public static class FacilityDependencyProtocol
{
    public static FacilityDependencyIntent Intent(ReservationFacilityDependency row) => new(row.Id, row.WorkflowId,
        row.Purpose, row.TenantId, row.SiteId, row.SlotId, row.IntentFingerprint, row.Deadline);

    public static string Fingerprint(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    public static string SerializeSnapshot(FacilityResourceSnapshot? snapshot, FacilityDependencyIntent intent)
    {
        if (snapshot is null || !snapshot.Complete || snapshot.Booking is null || snapshot.Resources is null || snapshot.DynamicBackups is null ||
            snapshot.Booking.TenantId != intent.TenantId || snapshot.Booking.SiteId != intent.SiteId ||
            snapshot.Booking.SlotId != intent.SlotId || string.IsNullOrWhiteSpace(snapshot.Booking.Version) ||
            !snapshot.Booking.ParticipatesInStructureGuard ||
            snapshot.Resources.Any(r => r is null || r.SlotId == Guid.Empty || r.SpatialUnitId == Guid.Empty ||
                string.IsNullOrWhiteSpace(r.VehicleType) || r.AncestorUnitIds is null || r.CompatibleVehicleTypes is null ||
                r.AncestorUnitIds.Any(id => id == Guid.Empty)) ||
            snapshot.Resources.Select(r => r.SlotId).Distinct().Count() != snapshot.Resources.Count ||
            snapshot.DynamicBackups.Any(b => b is null || b.SpatialUnitId == Guid.Empty || string.IsNullOrWhiteSpace(b.VehicleType) || b.Count < 0) ||
            snapshot.DynamicBackups.Select(b => (b.SpatialUnitId, b.VehicleType)).Distinct().Count() != snapshot.DynamicBackups.Count)
            throw BookingContracts.InvalidOwner();
        if (snapshot.Booking.EligibleCarSlot && !snapshot.Resources.Any(r => r.SlotId == intent.SlotId &&
            r.VehicleType == "CAR" && r.Active && r.Reachable)) throw BookingContracts.InvalidOwner();
        // Compare semantic resource ordering, not arbitrary producer array ordering.
        return JsonSerializer.Serialize(snapshot with
        {
            Resources = snapshot.Resources.OrderBy(r => r.SlotId).ToArray(),
            DynamicBackups = snapshot.DynamicBackups.OrderBy(b => b.SpatialUnitId).ThenBy(b => b.VehicleType, StringComparer.Ordinal).ToArray()
        });
    }
}
