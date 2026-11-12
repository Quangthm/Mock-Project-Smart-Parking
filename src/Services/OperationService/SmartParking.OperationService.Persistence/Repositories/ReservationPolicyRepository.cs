using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationPolicyRepository(ReservationDbContext db) : IReservationPolicyRepository
{
    public async Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        var rows = await db.Set<OperationalPolicyRow>().AsNoTracking().Where(x => x.IsActive && x.EffectiveFrom <= at &&
            (x.EffectiveUntil == null || at < x.EffectiveUntil) &&
            (x.TenantId == null && x.SiteId == null || x.TenantId == tenantId && x.SiteId == siteId)).ToListAsync(ct);
        return rows.Select(ToRevision).ToArray();
    }

    private static ReservationPolicyRevision ToRevision(OperationalPolicyRow row)
    {
        var values = new Dictionary<string, int>();
        void Add(string key, int? value, int scale = 1) { if (value is { } v) values.Add(key, checked(v * scale)); }
        Add("RESERVATION_MIN_DURATION", row.MinReservationDurationMinutes);
        Add("RESERVATION_MAX_DURATION", row.MaxReservationDurationHours, 60);
        Add("MAX_ADVANCE_BOOKING", row.MaxAdvanceBookingDays, 1440);
        Add("INTER_BOOKING_BUFFER", row.InterBookingBufferMinutes);
        Add("PAYMENT_HOLD_DURATION", row.PaymentHoldDurationMinutes);
        Add("RESERVATION_PROTECTION_WINDOW", row.ReservationProtectionWindowHours, 60);
        Add("ALLOCATION_LEAD_TIME", row.AllocationLeadTimeMinutes);
        Add("FACILITY_FAILURE_REFUND_LEAD_TIME", row.FacilityFailureRefundLeadMinutes);
        Add("OCCUPANCY_VALIDITY_TIMESPAN", row.OccupancyValidityTimespanHours, 60);
        Add("ARRIVAL_LATE_TOLERANCE", row.ArrivalLateToleranceMinutes);
        Add("RAPID_CANCELLATION_COUNT", row.RapidCancellationCount);
        Add("RAPID_CANCELLATION_WINDOW", row.RapidCancellationWindowMinutes);
        Add("RAPID_CANCELLATION_RESTRICTION_DURATION", row.RapidCancellationRestrictionMinutes);
        Add("DAILY_CANCELLATION_LIMIT", row.DailyCancellationLimit);
        var bounds = row.OverrideBounds is null ? new Dictionary<string, PolicyBound>() :
            JsonSerializer.Deserialize<Dictionary<string, PolicyBound>>(row.OverrideBounds,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new ReservationPolicyUnavailableException("Policy bounds are invalid.");
        return new(row.Id, row.Version, row.TenantId, row.SiteId, values, bounds, row.ArrivalLateToleranceEnabled, row.CancellationTimezone);
    }
}
