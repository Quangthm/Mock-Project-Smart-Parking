using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

// The caller owns the transaction and supplies either protected writer scope or a read-only snapshot.
internal sealed class CapacityFactsQuery(ReservationDbContext db)
{
    public async Task<ReservationAvailabilityFacts> ReadAsync(Guid tenantId, Guid siteId, DateTimeOffset start,
        DateTimeOffset? end, DateTimeOffset at, string? quotaCategory, CancellationToken ct)
    {
        var sessions = await db.ParkingSessions.AsNoTracking().Where(s => s.TenantId == tenantId &&
            s.SiteId == siteId && s.ExitTime == null).ToArrayAsync(ct);
        var upperBound = end ?? start;
        var allocations = await db.SlotAllocations.AsNoTracking().Where(a => a.TenantId == tenantId &&
            a.SiteId == siteId && (end.HasValue ? a.AllocatedStartTime < upperBound : a.AllocatedStartTime <= upperBound) &&
            a.AllocatedEndTime > start && (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED")).ToArrayAsync(ct);
        // An effective assignment can outlast its booking. Include its owner even when the booking window has ended.
        var owners = allocations.Where(a => a.ReservationId.HasValue).Select(a => a.ReservationId!.Value).Distinct().ToArray();
        var rows = await ReservationRepository.EffectiveCommitments(db, siteId, start, end, at, owners)
            .Where(r => r.TenantId == tenantId).ToArrayAsync(ct);
        var quota = quotaCategory is null ? null : await ReadQuotaAsync(tenantId, siteId, quotaCategory, ct);
        return new(rows, sessions, allocations, quota);
    }

    private async Task<int?> ReadQuotaAsync(Guid tenantId, Guid siteId, string quotaCategory, CancellationToken ct)
    {
        var quotas = await db.Set<ReservationQuotaConfig>().AsNoTracking().Where(q =>
            q.TenantId == tenantId && q.SiteId == siteId && q.VehicleType == quotaCategory && q.IsActive)
            .Select(q => q.MaxReservationQuota).Take(2).ToArrayAsync(ct);
        if (quotas.Length > 1)
            throw new ReservationGateException(503, "RESERVATION_QUOTA_CONFIGURATION_INVALID",
                "Multiple active reservation quotas apply to this site and vehicle category.");
        return quotas.Length == 0 ? null : quotas[0];
    }
}
