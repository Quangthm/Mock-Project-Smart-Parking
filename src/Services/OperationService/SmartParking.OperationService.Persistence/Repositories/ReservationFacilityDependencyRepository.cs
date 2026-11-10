using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationFacilityDependencyRepository(ReservationDbContext db) : IReservationFacilityDependencyRepository
{
    public void RequireNoTransaction()
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Remote Facility dependency calls cannot run inside a database transaction.");
    }

    public Task<ReservationFacilityDependency?> FindAsync(Guid token, CancellationToken ct) =>
        db.Set<ReservationFacilityDependency>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == token, ct);

    public Task<ReservationFacilityDependency?> FindByWorkflowPurposeAsync(Guid workflowId, string purpose, CancellationToken ct) =>
        db.Set<ReservationFacilityDependency>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkflowId == workflowId && x.Purpose == purpose, ct);

    public async Task<ReservationFacilityDependency?> LockAsync(Guid token, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Facility dependency transitions require a UoW transaction.");
        var row = await db.Set<ReservationFacilityDependency>().FromSqlInterpolated(
            $"SELECT * FROM reservation_facility_dependencies WHERE id = {token} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }

    public async Task<IReadOnlyList<Guid>> DueAsync(DateTimeOffset now, int take, CancellationToken ct) =>
        await db.Set<ReservationFacilityDependency>().AsNoTracking()
            .Where(x => x.NextActionAt <= now && (x.State == "PREPARING" || x.State == "READY" ||
                x.State == "ABORTING" || x.State == "HELD" && x.Deadline <= now))
            .OrderBy(x => x.NextActionAt).ThenBy(x => x.Id).Select(x => x.Id).Take(take).ToListAsync(ct);

    public void Add(ReservationFacilityDependency dependency) => db.Set<ReservationFacilityDependency>().Add(dependency);
}
