using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class AdvanceFacilityDependencyHandler(IReservationFacilityDependencyRepository dependencies,
    IReservationFacilityDependencyOwner owner, IReservationBookingRepository bookings, IUnitOfWork work, IMediator mediator)
    : IRequestHandler<AdvanceFacilityDependencyCommand>
{
    public async Task Handle(AdvanceFacilityDependencyCommand command, CancellationToken ct)
    {
        // Do not dispatch from an enclosing consumer/booking transaction: all remote effects
        // occur after reading committed intent and before starting the local response transaction.
        dependencies.RequireNoTransaction();
        var observed = await dependencies.FindAsync(command.Token, ct);
        if (observed is null || observed.State == "COMPLETED") return;
        var now = await bookings.DatabaseNowAsync(ct);
        if (observed.State is ("PREPARING" or "HELD") && now >= observed.Deadline)
        {
            await using var fence = await work.BeginTransactionAsync(ct);
            var pending = await dependencies.LockAsync(observed.Id, ct);
            now = await bookings.DatabaseNowAsync(ct);
            if (pending is not null && pending.State is ("PREPARING" or "HELD") && now >= pending.Deadline)
            {
                pending.BeginAbort("DEPENDENCY_DEADLINE_REACHED", now);
                await work.SaveChangesAsync(ct);
                await fence.CommitAsync(ct);
            }
            return;
        }
        if (observed.State == "HELD") return; // The booking writer owns local acceptance.

        var intent = FacilityDependencyProtocol.Intent(observed);
        FacilityDependencyResult? acquired = null;
        FacilityDependencyAcknowledgement? acknowledgement = null;
        switch (observed.State)
        {
            case "PREPARING":
                acquired = await owner.AcquireAsync(intent, ct);
                if (acquired.AcceptedIntent != intent || acquired.State is not ("HELD" or "UNKNOWN" or "ABORTED") ||
                    acquired.State != "HELD" && acquired.Snapshot is not null) throw BookingContracts.InvalidOwner();
                if (acquired.State == "UNKNOWN")
                {
                    await mediator.Send(new ScheduleFacilityDependencyRetryCommand(observed.Id), ct);
                    return;
                }
                break;
            case "READY":
                acknowledgement = await owner.CompleteAsync(intent, observed.OutcomeJson!, ct);
                ValidateAcknowledgement(acknowledgement, intent, "COMPLETED", FacilityDependencyProtocol.Fingerprint(observed.OutcomeJson!));
                break;
            case "ABORTING":
                acknowledgement = await owner.AbortAsync(intent, ct);
                ValidateAcknowledgement(acknowledgement, intent, "ABORTED", null);
                break;
            default:
                throw new InvalidOperationException("Unknown persisted Facility dependency state.");
        }

        // Remote errors leave the durable token untouched for replay. A response is never
        // allowed to resurrect a preparation that another transaction fenced or completed.
        await using var transaction = await work.BeginTransactionAsync(ct);
        var current = await dependencies.LockAsync(observed.Id, ct)
            ?? throw new InvalidOperationException("Durable Facility dependency disappeared.");
        now = await bookings.DatabaseNowAsync(ct);
        if (current.State != observed.State)
        {
            if (acquired?.State == "HELD" && current.SnapshotFingerprint is not null)
            {
                var replay = FacilityDependencyProtocol.SerializeSnapshot(acquired.Snapshot, intent);
                if (FacilityDependencyProtocol.Fingerprint(replay) != current.SnapshotFingerprint)
                    throw BookingContracts.InvalidOwner();
            }
            return;
        }
        if (acquired is not null)
        {
            if (acquired.State == "ABORTED")
            {
                current.BeginAbort("FACILITY_DEPENDENCY_ABORTED", now);
                current.Acknowledge("ABORTED", now);
            }
            else if (now >= current.Deadline) current.BeginAbort("DEPENDENCY_DEADLINE_REACHED", now);
            else
            {
                var snapshot = FacilityDependencyProtocol.SerializeSnapshot(acquired.Snapshot, intent);
                current.RecordHeld(snapshot, FacilityDependencyProtocol.Fingerprint(snapshot), now);
            }
        }
        else current.Acknowledge(observed.State == "READY" ? "COMMITTED" : "ABORTED", now);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static void ValidateAcknowledgement(FacilityDependencyAcknowledgement acknowledgement,
        FacilityDependencyIntent intent, string expectedState, string? outcomeFingerprint)
    {
        if (acknowledgement.AcceptedIntent != intent || acknowledgement.State != expectedState ||
            acknowledgement.OutcomeFingerprint != outcomeFingerprint) throw BookingContracts.InvalidOwner();
    }
}

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
