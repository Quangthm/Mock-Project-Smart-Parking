using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

// Consumes FacilityMaintenanceChangedV1 asynchronously: updates the local closure projection
// under the Operations site guard with monotonic revision / CLEARED tombstone fencing, then
// reconciles any overlapping bookings outside the projection lock.
public sealed class ApplyFacilityMaintenanceChangedCommandHandler(
    IEmergencyClosureReconciliationStore store,
    IReservationBookingRepository bookings,
    IEmergencyReallocationSlotProvider slots,
    IReservationAvailabilityRepository availability,
    IOperationsConcurrency guards,
    IReservationCommercialOwner commercial,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<ApplyFacilityMaintenanceChangedCommand>
{
    public async Task Handle(ApplyFacilityMaintenanceChangedCommand command, CancellationToken ct)
    {
        if (await store.HasProcessedEventAsync(command.EventId, ct)) return;

        var now = clock.GetUtcNow();
        var (shouldReconcile, recordedEarly) = await ApplyProjectionAsync(command, now, ct);
        if (recordedEarly) return;

        if (shouldReconcile)
        {
            await CapacityClosureReconciler.ReconcileAsync(
                new CapacityWithdrawalSpec(
                    command.MaintenanceId,
                    command.TenantId,
                    command.SiteId,
                    command.Scope,
                    command.UnitId,
                    command.ScopedUnitIds,
                    command.SlotIds,
                    command.StartAtUtc,
                    command.EndAtUtc),
                store, bookings, slots, availability, guards, commercial, work, clock, ct);
        }

        // Written last so a transient Payment failure lets the broker retry and finish refunds idempotently.
        await store.RecordProcessedEventAsync(new FacilityEventReceiptRow
        {
            EventId = command.EventId,
            EventType = "FacilityMaintenanceChangedV1",
            SiteId = command.SiteId,
            ProcessedAt = clock.GetUtcNow()
        }, ct);
    }

    private async Task<(bool ShouldReconcile, bool RecordedEarly)> ApplyProjectionAsync(
        ApplyFacilityMaintenanceChangedCommand command,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(command.SiteId, ct);

        var existing = await store.FindMaintenanceClosureAsync(command.MaintenanceId, ct);
        if (existing is not null &&
            (existing.TenantId != command.TenantId || existing.SiteId != command.SiteId))
        {
            await CommitReceiptAsync(command, now, ct, transaction);
            return (false, true);
        }

        // Ignore older revisions, or a duplicate revision carried by a different transport event id.
        if (existing is not null &&
            (command.Revision < existing.Revision ||
             (command.Revision == existing.Revision && existing.EnforcementRef != command.EventId)))
        {
            await CommitReceiptAsync(command, now, ct, transaction);
            return (false, true);
        }

        var slotId = command.Scope == "SLOT" && command.SlotIds.Count == 1
            ? command.SlotIds[0]
            : (Guid?)null;

        if (command.ChangeType == "CLEARED")
        {
            if (existing is null)
            {
                // Persist a cleared tombstone so any older UPSERTED arriving later is fenced.
                store.AddMaintenanceClosure(new StructureMaintenanceClosureRow
                {
                    WindowId = command.MaintenanceId,
                    TenantId = command.TenantId,
                    SiteId = command.SiteId,
                    UnitId = command.UnitId,
                    SlotId = slotId,
                    ScopedUnitIds = SerializeIds(command.ScopedUnitIds),
                    ScopedSlotIds = SerializeIds(command.SlotIds),
                    WindowStart = command.StartAtUtc,
                    WindowEnd = command.EndAtUtc,
                    Reason = command.Reason,
                    EnforcementRef = command.EventId,
                    AcknowledgedAt = now,
                    CancelledAt = command.OccurredAtUtc,
                    Revision = command.Revision
                });
            }
            else if (command.Revision > existing.Revision)
            {
                existing.Revision = command.Revision;
                existing.CancelledAt ??= command.OccurredAtUtc;
                existing.EnforcementRef = command.EventId;
                existing.AcknowledgedAt = now;
            }

            await CommitReceiptAsync(command, now, ct, transaction);
            return (false, true);
        }

        // Once cleared, a maintenance id can never be reinstated by a later delivery.
        if (existing is { CancelledAt: not null })
        {
            await CommitReceiptAsync(command, now, ct, transaction);
            return (false, true);
        }

        if (existing is null)
        {
            store.AddMaintenanceClosure(new StructureMaintenanceClosureRow
            {
                WindowId = command.MaintenanceId,
                TenantId = command.TenantId,
                SiteId = command.SiteId,
                UnitId = command.UnitId,
                SlotId = slotId,
                ScopedUnitIds = SerializeIds(command.ScopedUnitIds),
                ScopedSlotIds = SerializeIds(command.SlotIds),
                WindowStart = command.StartAtUtc,
                WindowEnd = command.EndAtUtc,
                Reason = command.Reason,
                EnforcementRef = command.EventId,
                AcknowledgedAt = now,
                CancelledAt = null,
                Revision = command.Revision
            });
        }
        else if (command.Revision > existing.Revision)
        {
            existing.UnitId = command.UnitId;
            existing.SlotId = slotId;
            existing.ScopedUnitIds = SerializeIds(command.ScopedUnitIds);
            existing.ScopedSlotIds = SerializeIds(command.SlotIds);
            existing.WindowStart = command.StartAtUtc;
            existing.WindowEnd = command.EndAtUtc;
            existing.Reason = command.Reason;
            existing.EnforcementRef = command.EventId;
            existing.AcknowledgedAt = now;
            existing.Revision = command.Revision;
        }

        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (true, false);
    }

    private async Task CommitReceiptAsync(
        ApplyFacilityMaintenanceChangedCommand command,
        DateTimeOffset now,
        CancellationToken ct,
        IUnitOfWorkTransaction transaction)
    {
        await store.RecordProcessedEventAsync(new FacilityEventReceiptRow
        {
            EventId = command.EventId,
            EventType = "FacilityMaintenanceChangedV1",
            SiteId = command.SiteId,
            ProcessedAt = now
        }, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static string? SerializeIds(IReadOnlyList<Guid> ids)
        => ids.Count == 0 ? null : JsonSerializer.Serialize(ids);
}
