using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class UnitOfWork(
    ReservationDbContext db,
    IReservationRepository reservations,
    IParkingSessionRepository parkingSessions,
    ISlotAllocationRepository slotAllocations, OperationsConcurrency guards, TimeProvider clock) : IUnitOfWork
{
    public IReservationRepository Reservations => reservations;
    public IParkingSessionRepository ParkingSessions => parkingSessions;
    public ISlotAllocationRepository SlotAllocations => slotAllocations;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        db.Database.CurrentTransaction is not null ? new JoinedTransaction() :
            new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));

    // The MassTransit EF consumer outbox owns the outer transaction when dispatching a message.
    // A joined handler must never commit/dispose that transaction ahead of inbox/outbox capture.
    private sealed class JoinedTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach (var entry in entries)
        {
            if (entry.Entity is not (ReservationRow or ParkingSessionRow or SlotAllocationRow or SiteCapacityPoolRow or StructureEditHoldRow or StructureSiteStateRow)) continue;
            var site = (Guid)entry.Property("SiteId").CurrentValue!;
            var tenant = (Guid)entry.Property("TenantId").CurrentValue!;
            if (!guards.HoldsSite(site)) throw new InvalidOperationException("Acquire the site guard before reading and changing commitments.");
            if (entry.State == EntityState.Modified && (!Equals(entry.Property("SiteId").OriginalValue, site) ||
                !Equals(entry.Property("TenantId").OriginalValue, tenant)))
                throw new InvalidOperationException("Commitment scope cannot change.");
            if (entry.Entity is StructureEditHoldRow or StructureSiteStateRow || entry.State == EntityState.Deleted) continue;
            if (await db.StructureEditHolds.AsNoTracking().AnyAsync(h => h.SiteId == site, cancellationToken)) throw new HoldConflictException();
            var live = entry.Entity switch
            {
                ReservationRow r => r.Status is "PENDING_PAYMENT" or "CONFIRMED" or "ALLOCATED" or "PARKING",
                ParkingSessionRow s => s.ExitTime is null,
                SlotAllocationRow a => a.AllocationStatus is "RESERVED" or "OCCUPIED",
                SiteCapacityPoolRow p => p.CurrentReservedCount > 0 || p.PendingPaymentCount > 0 || p.OccupiedCount > 0 || p.ProtectedCount > 0 || p.BackupCount > 0,
                _ => false
            };
            if (live && await db.StructureSiteStates.AsNoTracking().AnyAsync(s => s.SiteId == site && (!s.Active || s.TenantId != tenant), cancellationToken))
                throw new InvalidStructureEditException("Resource is inactive or belongs to another tenant.");
            if (entry.Entity is ReservationRow reservation)
            {
                if (entry.State == EntityState.Modified && entry.Property("ConfirmedAt").OriginalValue is DateTimeOffset confirmed && reservation.ConfirmedAt != confirmed)
                    throw new InvalidOperationException("First confirmation history is immutable.");
                if (entry.State == EntityState.Modified && entry.Property("AcceptedOperationalPolicySnapshot").OriginalValue is string accepted &&
                    reservation.AcceptedOperationalPolicySnapshot != accepted)
                    throw new InvalidOperationException("Accepted operational policies are immutable.");
                reservation.RecordFirstConfirmation(clock.GetUtcNow());
                if (entry.State == EntityState.Modified)
                    reservation.Version = checked((int)entry.Property("Version").OriginalValue! + 1);
            }
            if (entry.Entity is ParkingSessionRow session && entry.State == EntityState.Modified)
                session.Version = checked((int)entry.Property("Version").OriginalValue! + 1);
        }
        return await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
