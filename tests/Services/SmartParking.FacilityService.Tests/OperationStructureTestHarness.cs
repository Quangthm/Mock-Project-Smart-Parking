using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Domain;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Usecase.Structure.Commands.PrepareStructureEdit;
using SmartParking.OperationService.Application.Usecase.Structure.Commands.CompleteStructureEdit;
using SmartParking.OperationService.Application.Usecase.Structure.Commands.ForceReleaseStructureHolds;
using SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureSnapshot;
using OperationsUnitOfWork = SmartParking.OperationService.Persistence.Repositories.UnitOfWork;

namespace SmartParking.FacilityService.Tests;

internal sealed class OperationStructureTestHarness(ReservationDbContext db)
{
    public CapturingReservationFacts Facts { get; } = new();
    public static OperationsUnitOfWork Work(ReservationDbContext db, OperationsConcurrency guards) =>
        new(db, new ReservationRepository(db), new ParkingSessionRepository(db), new SlotAllocationRepository(db), guards, TimeProvider.System);
    // Each call models a new request scope, including a clean tracker after rolled-back writes.
    public Task<HoldSnapshot> PrepareAsync(Guid tenant, Guid site, Guid token, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var guards = new OperationsConcurrency(db);
        return new PrepareStructureEditCommandHandler(new StructureRepository(db), guards, Work(db, guards), TimeProvider.System)
            .Handle(new(tenant, site, token), ct);
    }
    public Task CompleteAsync(Guid tenant, Guid site, Guid token, EditOutcome? outcome, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var guards = new OperationsConcurrency(db);
        return new CompleteStructureEditCommandHandler(new StructureRepository(db), guards, Work(db, guards), TimeProvider.System, Facts)
            .Handle(new(tenant, site, token, outcome), ct);
    }
    public Task<HoldSnapshot> ReadSnapshotAsync(Guid tenant, Guid site, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var guards = new OperationsConcurrency(db);
        return new ReadStructureSnapshotQueryHandler(new StructureRepository(db), guards, Work(db, guards), TimeProvider.System)
            .Handle(new(tenant, site), ct);
    }
    public Task ForceReleaseAsync(Guid tenant, Guid site, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var guards = new OperationsConcurrency(db);
        return new ForceReleaseStructureHoldsCommandHandler(new StructureRepository(db), guards, Work(db, guards), TimeProvider.System)
            .Handle(new(tenant, site), ct);
    }
}
