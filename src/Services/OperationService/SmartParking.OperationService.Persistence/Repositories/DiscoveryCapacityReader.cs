using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

internal sealed class DiscoveryCapacityReader(ReservationDbContext db, IReservationPolicyRepository policies)
    : IDiscoveryCapacityReader
{
    public async Task<CarCapacityRead> ReadCarsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<Guid> candidateSlots,
        ReservationInterval? requestedStay, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || siteId == Guid.Empty)
            throw new ArgumentException("Trusted tenant and site are required for a capacity read.");

        ReservationAvailabilityFacts facts;
        ReservationInterval? effective = null;
        TimeSpan projection = TimeSpan.Zero;
        DateTimeOffset at;
        await using (var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct))
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
            at = await db.Database.SqlQuery<DateTimeOffset>($"SELECT statement_timestamp() AS \"Value\"").SingleAsync(ct);
            var state = await db.StructureSiteStates.AsNoTracking().SingleOrDefaultAsync(s => s.SiteId == siteId, ct);
            if (state is not null && state.TenantId != tenantId ||
                await db.StructureEditHolds.AnyAsync(h => h.SiteId == siteId, ct))
                return new(at, null);
            if (state is { Active: false }) return new(at, 0);

            try
            {
                if (requestedStay is not null)
                {
                    var revisions = await policies.ReadEffectiveAsync(tenantId, siteId, at, ct);
                    var policy = ReservationPolicyResolver.ResolveForSite(revisions, tenantId, siteId);
                    if (!policy.AllowsWindow(requestedStay, at)) return new(at, 0);
                    effective = new(requestedStay.Start, requestedStay.End.AddMinutes(policy.Values["INTER_BOOKING_BUFFER"]));
                    projection = TimeSpan.FromMinutes(policy.Values["OCCUPANCY_VALIDITY_TIMESPAN"]);
                }
                facts = await new CapacityFactsQuery(db).ReadAsync(tenantId, siteId, effective?.Start ?? at,
                    effective?.End, at, effective is null ? null : "CAR", ct);
            }
            catch (ReservationPolicyUnavailableException) { return new(at, null); }
            catch (ReservationGateException error) when (error.Code == "RESERVATION_QUOTA_CONFIGURATION_INVALID")
            {
                return new(at, null);
            }
            await snapshot.CommitAsync(ct);
        }

        return new(at, facts.CountAvailableCars(candidateSlots, effective, at, projection, ct));
    }
}
