using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class LotPolicyRepository(ReservationDbContext db, IOperationsConcurrency? guards = null) : ILotPolicyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static string ComputeIdempotencyHash(Guid siteId, string mode, DateTimeOffset? scheduledAt, string? note, string? payload)
    {
        var raw = $"{siteId:N}:{mode.ToUpperInvariant()}:{scheduledAt?.ToString("O")}:{note}:{payload}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static void ValidatePlatformOverrideBounds(PolicyDraftConfig config, OperationalPolicyRow platform, int? previousDriverLimit)
    {
        var bounds = ParseCanonicalBounds(platform.OverrideBounds);

        void Check(string key, int? draftValue, int scale, int? platformValue)
        {
            if (draftValue is not { } value) return;
            var scaled = (long)value * scale;
            var platformCanonical = (long?)platformValue * scale;
            if (platformCanonical is { } canonical && scaled == canonical) return;
            if (bounds.TryGetValue(key, out var bound))
            {
                if (scaled < bound.Minimum || scaled > bound.Maximum)
                    throw new PolicyOutOfBoundsException(key,
                        $"{key} value {scaled} minutes is outside the approved bounds {bound.Minimum}..{bound.Maximum} minutes.");
                return;
            }
            // Retain a previously published legacy value only when the platform has no bound.
            if (key == PolicyCompatibility.DriverReallocationRequestLimitKey &&
                previousDriverLimit == value &&
                value is >= PolicyCompatibility.DriverReallocationLegacyMin and <= PolicyCompatibility.DriverReallocationLegacyMax)
                return;

            throw new PolicyOutOfBoundsException(key,
                $"{key} has no approved owner override; the requested value {scaled} minutes is not permitted.");
        }

        Check("RESERVATION_MIN_DURATION", config.MinReservationDurationMinutes, 1, platform.MinReservationDurationMinutes);
        Check("RESERVATION_MAX_DURATION", config.MaxReservationDurationHours, 60, platform.MaxReservationDurationHours);
        Check("MAX_ADVANCE_BOOKING", config.MaxAdvanceBookingDays, 1440, platform.MaxAdvanceBookingDays);
        Check("INTER_BOOKING_BUFFER", config.InterBookingBufferMinutes, 1, platform.InterBookingBufferMinutes);
        Check("PAYMENT_HOLD_DURATION", config.PaymentHoldDurationMinutes, 1, platform.PaymentHoldDurationMinutes);
        Check("RESERVATION_PROTECTION_WINDOW", config.ReservationProtectionWindowHours, 60, platform.ReservationProtectionWindowHours);
        Check("ALLOCATION_LEAD_TIME", config.AllocationLeadTimeMinutes, 1, platform.AllocationLeadTimeMinutes);
        Check("FACILITY_FAILURE_REFUND_LEAD_TIME", config.FacilityFailureRefundLeadMinutes, 1, platform.FacilityFailureRefundLeadMinutes);
        Check("OCCUPANCY_VALIDITY_TIMESPAN", config.OccupancyValidityTimespanHours, 60, platform.OccupancyValidityTimespanHours);
        Check("ARRIVAL_LATE_TOLERANCE", config.ArrivalLateToleranceMinutes, 1, platform.ArrivalLateToleranceMinutes);
        Check("DRIVER_REALLOCATION_REQUEST_LIMIT", config.DriverReallocationRequestLimit, 1, platform.DriverReallocationRequestLimit);
    }

    // Per-field bounds checking cannot catch cross-field invariants of the composed (post-inheritance)
    // configuration. Build the effective values (site draft override over the platform revision) and
    // reject publication before any write when an invariant is violated, for example when the
    // effective site maximum reservation duration falls below the platform-inherited minimum.
    private static void ValidateComposedInvariants(PolicyDraftConfig config, OperationalPolicyRow platform)
    {
        var minMinutes = config.MinReservationDurationMinutes ?? platform.MinReservationDurationMinutes;
        var maxMinutes = (long?)(config.MaxReservationDurationHours ?? platform.MaxReservationDurationHours) * 60;

        if (minMinutes is { } min)
        {
            if (min <= 0)
                throw new PolicyOutOfBoundsException("RESERVATION_MIN_DURATION",
                    $"Effective minimum reservation duration {min} minutes must be greater than zero.");
            if (maxMinutes is { } max && max < min)
                throw new PolicyOutOfBoundsException("RESERVATION_MAX_DURATION",
                    $"Effective maximum reservation duration {max} minutes is less than the effective minimum {min} minutes.");
        }
        else if (maxMinutes is { } maxOnly && maxOnly <= 0)
        {
            throw new PolicyOutOfBoundsException("RESERVATION_MAX_DURATION",
                "Effective maximum reservation duration must be greater than zero.");
        }

        var paymentHoldMinutes = config.PaymentHoldDurationMinutes ?? platform.PaymentHoldDurationMinutes;
        if (paymentHoldMinutes is { } hold && hold <= 0)
            throw new PolicyOutOfBoundsException("PAYMENT_HOLD_DURATION",
                $"Effective payment hold duration {hold} minutes must be greater than zero.");

        var lateToleranceMinutes = config.ArrivalLateToleranceMinutes ?? platform.ArrivalLateToleranceMinutes;
        if (lateToleranceMinutes is { } tolerance && (tolerance < 15 || tolerance > 30))
            throw new PolicyOutOfBoundsException("ARRIVAL_LATE_TOLERANCE",
                $"Effective arrival late tolerance {tolerance} minutes must be between 15 and 30 minutes.");
    }

    private static Dictionary<string, PolicyBound> ParseCanonicalBounds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, PolicyBound>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, PolicyBound>>(json, JsonOptions)
                ?? new Dictionary<string, PolicyBound>();
        }
        catch
        {
            return new Dictionary<string, PolicyBound>();
        }
    }

    public async Task<PolicyCatalogueData> GetCatalogueAsync(Guid siteId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var platform = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.SiteId == null && x.IsActive &&
                        x.EffectiveFrom <= now && (x.EffectiveUntil == null || now < x.EffectiveUntil))
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        var siteActive = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom <= now && (x.EffectiveUntil == null || now < x.EffectiveUntil))
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        var hasDraft = await db.OperationalPolicyDrafts
            .AsNoTracking()
            .AnyAsync(x => x.SiteId == siteId, ct);

        var adminBounds = new Dictionary<string, AdminBounds>();
        if (!string.IsNullOrWhiteSpace(platform?.OverrideBounds))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, SmartParking.OperationService.Domain.Reservations.PolicyBound>>(platform.OverrideBounds, JsonOptions);
                if (parsed != null)
                {
                    adminBounds = parsed.ToDictionary(k => k.Key, v => new AdminBounds(v.Value.Minimum, v.Value.Maximum));
                }
            }
            catch
            {
                adminBounds = [];
            }
        }

        static int CeilDiv(int value, int divisor) => value <= 0 ? 0 : 1 + (value - 1) / divisor;
        static int FloorDiv(int value, int divisor) => value < 0 ? 0 : value / divisor;

        AdminBounds? GetBound(string key, int divisor, int defaultMin, int defaultMax)
        {
            if (platform is null) return new AdminBounds(defaultMin, defaultMax);
            if (!adminBounds.TryGetValue(key, out var b)) return null;
            var displayMin = CeilDiv(b.Min, divisor);
            var displayMax = FloorDiv(b.Max, divisor);
            return displayMin > displayMax ? null : new AdminBounds(displayMin, displayMax);
        }

        var policies = new List<PolicyCatalogueItem>
        {
            new(
                "RESERVATION_MIN_DURATION",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                platform?.MinReservationDurationMinutes ?? 15,
                GetBound("RESERVATION_MIN_DURATION", 1, 15, 60),
                siteActive?.MinReservationDurationMinutes,
                siteActive?.MinReservationDurationMinutes ?? platform?.MinReservationDurationMinutes ?? 15,
                siteActive?.MinReservationDurationMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("RESERVATION_MIN_DURATION", 1, 15, 60) != null
            ),
            new(
                "RESERVATION_MAX_DURATION",
                "RESERVATION_WINDOW",
                "integer",
                "hours",
                platform?.MaxReservationDurationHours ?? 24,
                GetBound("RESERVATION_MAX_DURATION", 60, 1, 168),
                siteActive?.MaxReservationDurationHours,
                siteActive?.MaxReservationDurationHours ?? platform?.MaxReservationDurationHours ?? 24,
                siteActive?.MaxReservationDurationHours != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("RESERVATION_MAX_DURATION", 60, 1, 168) != null
            ),
            new(
                "MAX_ADVANCE_BOOKING",
                "ADVANCE_BOOKING",
                "integer",
                "days",
                platform?.MaxAdvanceBookingDays ?? 7,
                GetBound("MAX_ADVANCE_BOOKING", 1440, 1, 30),
                siteActive?.MaxAdvanceBookingDays,
                siteActive?.MaxAdvanceBookingDays ?? platform?.MaxAdvanceBookingDays ?? 7,
                siteActive?.MaxAdvanceBookingDays != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("MAX_ADVANCE_BOOKING", 1440, 1, 30) != null
            ),
            new(
                "INTER_BOOKING_BUFFER",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                platform?.InterBookingBufferMinutes ?? 0,
                GetBound("INTER_BOOKING_BUFFER", 1, 0, 60),
                siteActive?.InterBookingBufferMinutes,
                siteActive?.InterBookingBufferMinutes ?? platform?.InterBookingBufferMinutes ?? 0,
                siteActive?.InterBookingBufferMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("INTER_BOOKING_BUFFER", 1, 0, 60) != null
            ),
            new(
                "PAYMENT_HOLD_DURATION",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                platform?.PaymentHoldDurationMinutes ?? 15,
                GetBound("PAYMENT_HOLD_DURATION", 1, 5, 30),
                siteActive?.PaymentHoldDurationMinutes,
                siteActive?.PaymentHoldDurationMinutes ?? platform?.PaymentHoldDurationMinutes ?? 15,
                siteActive?.PaymentHoldDurationMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("PAYMENT_HOLD_DURATION", 1, 5, 30) != null
            ),
            new(
                "ARRIVAL_LATE_TOLERANCE",
                "CHECKIN_TOLERANCE",
                "integer",
                "minutes",
                platform?.ArrivalLateToleranceMinutes ?? 15,
                GetBound("ARRIVAL_LATE_TOLERANCE", 1, 15, 30),
                siteActive?.ArrivalLateToleranceMinutes,
                siteActive?.ArrivalLateToleranceMinutes ?? platform?.ArrivalLateToleranceMinutes ?? 15,
                siteActive?.ArrivalLateToleranceMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("ARRIVAL_LATE_TOLERANCE", 1, 15, 30) != null
            ),
            new(
                "ARRIVAL_LATE_TOLERANCE_ENABLED",
                "CHECKIN_TOLERANCE",
                "boolean",
                "flag",
                platform?.ArrivalLateToleranceEnabled ?? true,
                null,
                siteActive?.ArrivalLateToleranceEnabled,
                siteActive?.ArrivalLateToleranceEnabled ?? platform?.ArrivalLateToleranceEnabled ?? true,
                siteActive?.ArrivalLateToleranceEnabled != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                true
            ),
            new(
                "RESERVATION_PROTECTION_WINDOW",
                "RESERVATION_WINDOW",
                "integer",
                "hours",
                platform?.ReservationProtectionWindowHours ?? 4,
                GetBound("RESERVATION_PROTECTION_WINDOW", 60, 1, 12),
                siteActive?.ReservationProtectionWindowHours,
                siteActive?.ReservationProtectionWindowHours ?? platform?.ReservationProtectionWindowHours ?? 4,
                siteActive?.ReservationProtectionWindowHours != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("RESERVATION_PROTECTION_WINDOW", 60, 1, 12) != null
            ),
            new(
                "ALLOCATION_LEAD_TIME",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                platform?.AllocationLeadTimeMinutes ?? 60,
                GetBound("ALLOCATION_LEAD_TIME", 1, 15, 180),
                siteActive?.AllocationLeadTimeMinutes,
                siteActive?.AllocationLeadTimeMinutes ?? platform?.AllocationLeadTimeMinutes ?? 60,
                siteActive?.AllocationLeadTimeMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("ALLOCATION_LEAD_TIME", 1, 15, 180) != null
            ),
            new(
                "OCCUPANCY_VALIDITY_TIMESPAN",
                "RESERVATION_WINDOW",
                "integer",
                "hours",
                platform?.OccupancyValidityTimespanHours ?? 4,
                GetBound("OCCUPANCY_VALIDITY_TIMESPAN", 60, 1, 8),
                siteActive?.OccupancyValidityTimespanHours,
                siteActive?.OccupancyValidityTimespanHours ?? platform?.OccupancyValidityTimespanHours ?? 4,
                siteActive?.OccupancyValidityTimespanHours != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("OCCUPANCY_VALIDITY_TIMESPAN", 60, 1, 8) != null
            ),
            new(
                "DRIVER_REALLOCATION_REQUEST_LIMIT",
                "RESERVATION_WINDOW",
                "integer",
                "count",
                platform?.DriverReallocationRequestLimit ?? 2,
                GetBound("DRIVER_REALLOCATION_REQUEST_LIMIT", 1, 0, 5),
                siteActive?.DriverReallocationRequestLimit,
                siteActive?.DriverReallocationRequestLimit ?? platform?.DriverReallocationRequestLimit ?? 2,
                siteActive?.DriverReallocationRequestLimit != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("DRIVER_REALLOCATION_REQUEST_LIMIT", 1, 0, 5) != null
            ),
            new(
                "FACILITY_FAILURE_REFUND_LEAD_TIME",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                platform?.FacilityFailureRefundLeadMinutes ?? 15,
                GetBound("FACILITY_FAILURE_REFUND_LEAD_TIME", 1, 15, 60),
                siteActive?.FacilityFailureRefundLeadMinutes,
                siteActive?.FacilityFailureRefundLeadMinutes ?? platform?.FacilityFailureRefundLeadMinutes ?? 15,
                siteActive?.FacilityFailureRefundLeadMinutes != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                GetBound("FACILITY_FAILURE_REFUND_LEAD_TIME", 1, 15, 60) != null
            ),
            new(
                "ALTERNATIVE_OFFER_DURATION",
                "RESERVATION_WINDOW",
                "integer",
                "minutes",
                5,
                GetBound("ALTERNATIVE_OFFER_DURATION", 1, 3, 15),
                null,
                5,
                "ADMIN_DEFAULT",
                false
            ),
            new(
                "RESERVATION_MODE",
                "RESERVATION_WINDOW",
                "string",
                "enum",
                "SPECIFIC_SLOT",
                null,
                null,
                "SPECIFIC_SLOT",
                "ADMIN_DEFAULT",
                false
            ),
            new(
                "PARKING_PRODUCT_ENABLED",
                "PRODUCT_SUBSCRIPTION",
                "object",
                "boolean_flags",
                new { monthly = false, yearly = false },
                null,
                siteActive?.ParkingProductEnabled != null ? JsonSerializer.Deserialize<ParkingProductFlags>(siteActive.ParkingProductEnabled, JsonOptions) : null,
                siteActive?.ParkingProductEnabled != null ? JsonSerializer.Deserialize<ParkingProductFlags>(siteActive.ParkingProductEnabled, JsonOptions) : new { monthly = false, yearly = false },
                siteActive?.ParkingProductEnabled != null ? "OWNER_OVERRIDE" : "ADMIN_DEFAULT",
                true
            )
        };

        return new PolicyCatalogueData(
            SiteId: siteId,
            ActiveRevision: siteActive?.Version ?? 1,
            ActiveSince: siteActive?.EffectiveFrom ?? platform?.EffectiveFrom ?? DateTimeOffset.UtcNow,
            HasDraft: hasDraft,
            Policies: policies
        );
    }

    public async Task<PolicyDraftEntity?> GetDraftAsync(Guid siteId, CancellationToken ct)
    {
        var row = await db.OperationalPolicyDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);

        if (row is null) return null;

        return new PolicyDraftEntity(
            row.Id,
            row.TenantId,
            row.SiteId,
            row.TargetRevision,
            row.DraftPayload,
            row.ScheduledActivationAt,
            row.UpdatedAt,
            row.UpdatedBy
        );
    }

    public async Task<PolicyDraftEntity> SaveDraftAsync(
        Guid siteId,
        Guid tenantId,
        Guid actorId,
        PolicyDraftConfig config,
        DateTimeOffset? scheduledAt,
        CancellationToken ct)
    {
        var existing = await db.OperationalPolicyDrafts
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);

        var currentMaxVersion = await db.Set<OperationalPolicyRow>()
            .Where(x => x.SiteId == siteId)
            .Select(x => (int?)x.Version)
            .MaxAsync(ct) ?? 0;

        var targetRevision = currentMaxVersion + 1;
        var payloadJson = JsonSerializer.Serialize(config, JsonOptions);

        if (existing is not null)
        {
            existing.TenantId = tenantId;
            existing.DraftPayload = payloadJson;
            existing.TargetRevision = targetRevision;
            existing.ScheduledActivationAt = scheduledAt;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = actorId;
        }
        else
        {
            existing = new OperationalPolicyDraftRow
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                TenantId = tenantId,
                TargetRevision = targetRevision,
                DraftPayload = payloadJson,
                ScheduledActivationAt = scheduledAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = actorId
            };
            await db.OperationalPolicyDrafts.AddAsync(existing, ct);
        }

        await db.SaveChangesAsync(ct);

        return new PolicyDraftEntity(
            existing.Id,
            existing.TenantId,
            existing.SiteId,
            existing.TargetRevision,
            existing.DraftPayload,
            existing.ScheduledActivationAt,
            existing.UpdatedAt,
            existing.UpdatedBy
        );
    }

    public async Task<PolicyPublishResult> PublishDraftAsync(
        Guid siteId,
        Guid tenantId,
        Guid actorId,
        string mode,
        DateTimeOffset? scheduledAt,
        string? note,
        string? idempotencyKey,
        CancellationToken ct)
    {
        // A serialization failure or deadlock always rolls the whole transaction back, so the
        // database is never left partially written. Retry a bounded number of times; if the
        // conflict persists, surface it as a 409 instead of a raw provider exception.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await PublishDraftTransactionalAsync(siteId, tenantId, actorId, mode, scheduledAt, note,
                    idempotencyKey, ct);
            }
            catch (Exception exception) when (PolicyCompatibility.IsRetryableConflict(exception))
            {
                if (attempt >= PolicyCompatibility.MaxConflictRetries - 1)
                {
                    throw new PolicyOperationInProgressException(
                        "The policy publish was aborted by a conflicting concurrent write. Retry the publish.");
                }

                db.ChangeTracker.Clear();
                await Task.Delay(PolicyCompatibility.RetryDelay(attempt), ct);
            }
        }
    }

    private async Task<PolicyPublishResult> PublishDraftTransactionalAsync(
        Guid siteId,
        Guid tenantId,
        Guid actorId,
        string mode,
        DateTimeOffset? scheduledAt,
        string? note,
        string? idempotencyKey,
        CancellationToken ct)
    {
        // 1. Transaction, shared compatibility gate (owner and admin share one lock), then the
        //    site-scoped guard. The gate is acquired first so the two protocols never deadlock.
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await PolicyCompatibility.AcquireAsync(db, ct);
        if (guards is not null && db.Database.IsRelational())
        {
            await guards.LockSiteAsync(siteId, ct);
        }

        // 2. Draft lookup
        var draft = await db.OperationalPolicyDrafts
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);

        // 3. Idempotency Check (under lock & transaction)
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPublished = await db.Set<OperationalPolicyRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SiteId == siteId && x.IdempotencyKey == idempotencyKey, ct);

            if (existingPublished is not null)
            {
                var isReplayedScheduled = existingPublished.EffectiveFrom > existingPublished.CreatedAt;
                var existingMode = isReplayedScheduled ? "SCHEDULED" : "IMMEDIATE";

                // If a draft exists, verify full hash including draft payload
                if (draft is not null)
                {
                    var draftHash = ComputeIdempotencyHash(siteId, mode, scheduledAt, note, draft.DraftPayload);
                    if (existingPublished.IdempotencyPayloadHash is not null &&
                        !string.Equals(existingPublished.IdempotencyPayloadHash, draftHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IdempotencyPayloadMismatchException("Idempotency key was used previously with different payload.");
                    }
                }
                else
                {
                    // Draft was already consumed by the prior publish; verify request envelope matches
                    var modeMismatch = !string.Equals(mode, existingMode, StringComparison.OrdinalIgnoreCase);
                    var noteMismatch = !string.Equals(note ?? string.Empty, existingPublished.PublishNote ?? string.Empty, StringComparison.Ordinal);
                    var scheduleMismatch = isReplayedScheduled && scheduledAt.HasValue && scheduledAt.Value != existingPublished.EffectiveFrom;

                    if (modeMismatch || noteMismatch || scheduleMismatch)
                    {
                        throw new IdempotencyPayloadMismatchException("Idempotency key was used previously with different payload.");
                    }
                }

                return new PolicyPublishResult(
                    SiteId: siteId,
                    NewRevision: existingPublished.Version,
                    Status: isReplayedScheduled ? "SCHEDULED" : "ACTIVE",
                    EffectiveFrom: existingPublished.EffectiveFrom,
                    PublishedAt: existingPublished.CreatedAt,
                    PublishNote: existingPublished.PublishNote,
                    Message: isReplayedScheduled
                        ? $"Policy draft scheduled for activation at {existingPublished.EffectiveFrom:O}."
                        : $"Policy revision {existingPublished.Version} published and activated successfully.",
                    IsReplayed: true
                );
            }
        }

        // 4. If not a replay, a draft MUST exist to publish
        if (draft is null)
            throw new NoDraftToPublishException();

        var computedHash = !string.IsNullOrWhiteSpace(idempotencyKey)
            ? ComputeIdempotencyHash(siteId, mode, scheduledAt, note, draft.DraftPayload)
            : null;

        PolicyDraftConfig? config = null;
        try
        {
            config = JsonSerializer.Deserialize<PolicyDraftConfig>(draft.DraftPayload, JsonOptions);
        }
        catch
        {
            // fallback empty
        }

        config ??= new PolicyDraftConfig();

        // 4. Safe Boundary Transition
        var now = DateTimeOffset.UtcNow;
        var isScheduled = mode.Equals("SCHEDULED", StringComparison.OrdinalIgnoreCase);
        var effectiveFrom = isScheduled && scheduledAt.HasValue ? scheduledAt.Value : now;

        // Check ALL active/scheduled platform revisions whose effective intervals intersect the site policy interval.
        // Fail closed when no platform revision covers the site start, or when any intersecting platform bounds are violated.
        var intersectingPlatforms = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.SiteId == null && x.IsActive &&
                        (x.EffectiveUntil == null || x.EffectiveUntil > effectiveFrom))
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync(ct);

        var platform = intersectingPlatforms
            .Where(x => x.EffectiveFrom <= effectiveFrom && (x.EffectiveUntil == null || effectiveFrom < x.EffectiveUntil))
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();

        if (platform is null)
            throw new ReservationPolicyUnavailableException("Platform policy is required to publish site policy.");

        var previousDriverLimit = config.DriverReallocationRequestLimit.HasValue
            ? await db.Set<OperationalPolicyRow>().AsNoTracking()
                .Where(row => row.TenantId == tenantId && row.SiteId == siteId && row.IsActive &&
                    row.EffectiveFrom <= effectiveFrom && (row.EffectiveUntil == null || effectiveFrom < row.EffectiveUntil))
                .Select(row => row.DriverReallocationRequestLimit)
                .SingleOrDefaultAsync(ct)
            : null;

        foreach (var pRev in intersectingPlatforms)
        {
            ValidatePlatformOverrideBounds(config, pRev, previousDriverLimit);
            ValidateComposedInvariants(config, pRev);
        }

        var currentMaxVersion = await db.Set<OperationalPolicyRow>()
            .Where(x => x.SiteId == siteId)
            .Select(x => (int?)x.Version)
            .MaxAsync(ct) ?? 0;

        var newRevision = currentMaxVersion + 1;

        OperationalPolicyRow? active = null;
        if (isScheduled)
        {
            // Close / supersede any future scheduled policies after effectiveFrom to avoid range collisions
            var futureScheduled = await db.Set<OperationalPolicyRow>()
                .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom >= effectiveFrom)
                .ToListAsync(ct);
            foreach (var future in futureScheduled)
            {
                future.IsActive = false;
                future.UpdatedAt = now;
            }

            // Current active row is clamped to effectiveFrom (strictly < effectiveFrom to avoid zero-length interval)
            active = await db.Set<OperationalPolicyRow>()
                .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom < effectiveFrom && (x.EffectiveUntil == null || effectiveFrom < x.EffectiveUntil))
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefaultAsync(ct);

            if (active is not null)
            {
                if (effectiveFrom > active.EffectiveFrom)
                {
                    active.EffectiveUntil = effectiveFrom;
                }
                active.UpdatedAt = now;
            }
        }
        else
        {
            // Immediate activation:
            // 1. Close / supersede any future scheduled policies starting at or after 'now' to avoid range collisions
            var futureScheduled = await db.Set<OperationalPolicyRow>()
                .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom >= now)
                .ToListAsync(ct);
            foreach (var future in futureScheduled)
            {
                future.IsActive = false;
                future.UpdatedAt = now;
            }

            // 2. Close current active row (strictly < effectiveFrom to avoid zero-length interval)
            active = await db.Set<OperationalPolicyRow>()
                .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom < effectiveFrom && (x.EffectiveUntil == null || now < x.EffectiveUntil))
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefaultAsync(ct);

            if (active is not null)
            {
                active.IsActive = false;
                if (effectiveFrom > active.EffectiveFrom)
                {
                    active.EffectiveUntil = effectiveFrom;
                }
                active.UpdatedAt = now;
            }
        }

        var lateToleranceEnabled = config.ArrivalLateToleranceEnabled
            ?? (config.ArrivalLateToleranceMinutes.HasValue ? true : (active?.ArrivalLateToleranceEnabled ?? true));

        var newRow = new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Version = newRevision,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = null,
            PublishedBy = actorId,
            PublishNote = note,
            IdempotencyKey = idempotencyKey,
            IdempotencyPayloadHash = computedHash,
            PaymentHoldDurationMinutes = config.PaymentHoldDurationMinutes,
            ArrivalLateToleranceMinutes = config.ArrivalLateToleranceMinutes,
            ArrivalLateToleranceEnabled = lateToleranceEnabled,
            MinReservationDurationMinutes = config.MinReservationDurationMinutes,
            MaxReservationDurationHours = config.MaxReservationDurationHours,
            ReservationProtectionWindowHours = config.ReservationProtectionWindowHours,
            AllocationLeadTimeMinutes = config.AllocationLeadTimeMinutes,
            OccupancyValidityTimespanHours = config.OccupancyValidityTimespanHours,
            MaxAdvanceBookingDays = config.MaxAdvanceBookingDays,
            InterBookingBufferMinutes = config.InterBookingBufferMinutes,
            DriverReallocationRequestLimit = config.DriverReallocationRequestLimit,
            FacilityFailureRefundLeadMinutes = config.FacilityFailureRefundLeadMinutes,
            ParkingProductEnabled = config.ParkingProductEnabled != null ? JsonSerializer.Serialize(config.ParkingProductEnabled, JsonOptions) : null
        };

        await db.Set<OperationalPolicyRow>().AddAsync(newRow, ct);
        db.OperationalPolicyDrafts.Remove(draft);

        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return new PolicyPublishResult(
            SiteId: siteId,
            NewRevision: newRevision,
            Status: isScheduled ? "SCHEDULED" : "ACTIVE",
            EffectiveFrom: effectiveFrom,
            PublishedAt: now,
            PublishNote: note,
            Message: isScheduled
                ? $"Policy draft scheduled for activation at {effectiveFrom:O}."
                : $"Policy revision {newRevision} published and activated successfully.",
            IsReplayed: false
        );
    }

    public async Task<PagedResult<PolicyRevisionSummary>> GetRevisionsAsync(Guid siteId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.SiteId == siteId);

        var total = await query.CountAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var rows = await query
            .OrderByDescending(x => x.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            var status = r.EffectiveUntil != null && r.EffectiveUntil <= now
                ? "ARCHIVED"
                : r.IsActive && r.EffectiveFrom > now
                    ? "SCHEDULED"
                    : r.IsActive
                        ? "ACTIVE"
                        : "ARCHIVED";

            return new PolicyRevisionSummary(
                r.Version,
                status,
                r.EffectiveFrom,
                r.EffectiveUntil,
                r.PublishedBy ?? Guid.Empty,
                r.PublishNote
            );
        }).ToList();

        return new PagedResult<PolicyRevisionSummary>(items, page, pageSize, total);
    }

    public async Task<PolicySnapshot?> GetRevisionSnapshotAsync(Guid siteId, int version, CancellationToken ct)
    {
        var row = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Version == version, ct);

        if (row is null) return null;

        var now = DateTimeOffset.UtcNow;
        var status = row.EffectiveUntil != null && row.EffectiveUntil <= now
            ? "ARCHIVED"
            : row.IsActive && row.EffectiveFrom > now
                ? "SCHEDULED"
                : row.IsActive
                    ? "ACTIVE"
                    : "ARCHIVED";

        var dict = new Dictionary<string, object?>
        {
            ["paymentHoldDurationMinutes"] = row.PaymentHoldDurationMinutes,
            ["arrivalLateToleranceMinutes"] = row.ArrivalLateToleranceMinutes,
            ["arrivalLateToleranceEnabled"] = row.ArrivalLateToleranceEnabled,
            ["minReservationDurationMinutes"] = row.MinReservationDurationMinutes,
            ["maxReservationDurationHours"] = row.MaxReservationDurationHours,
            ["reservationProtectionWindowHours"] = row.ReservationProtectionWindowHours,
            ["allocationLeadTimeMinutes"] = row.AllocationLeadTimeMinutes,
            ["occupancyValidityTimespanHours"] = row.OccupancyValidityTimespanHours,
            ["maxAdvanceBookingDays"] = row.MaxAdvanceBookingDays,
            ["interBookingBufferMinutes"] = row.InterBookingBufferMinutes,
            ["driverReallocationRequestLimit"] = row.DriverReallocationRequestLimit,
            ["facilityFailureRefundLeadMinutes"] = row.FacilityFailureRefundLeadMinutes,
            ["parkingProductEnabled"] = row.ParkingProductEnabled != null
                ? JsonSerializer.Deserialize<ParkingProductFlags>(row.ParkingProductEnabled, JsonOptions)
                : null
        };

        return new PolicySnapshot(
            siteId,
            row.Version,
            status,
            row.EffectiveFrom,
            row.EffectiveUntil,
            row.PublishedBy ?? Guid.Empty,
            dict
        );
    }
}

/// <summary>
/// Shared serialisation protocol for the platform/site policy compatibility decision.
/// Owner publish (<see cref="LotPolicyRepository.PublishDraftAsync"/>) and platform activation
/// (<see cref="ReservationPolicyRepository.ActivateRevisionAsync"/>) both acquire this single
/// transaction-scoped PostgreSQL advisory lock BEFORE reading the revisions they validate against.
/// Without it the two paths lock different rows (<c>site_id</c> vs the platform revision) and may
/// both validate stale state before either commits. The lock is a no-op on non-relational
/// providers (EF InMemory) so unit tests remain provider-neutral.
/// </summary>
public static class PolicyCompatibility
{
    public const string LockKey = "reservation-policy-compatibility/v1";
    public const long LockNamespace = 7L;
    public const int MaxConflictRetries = 3;

    // The historical platform default and implicit accepted range for this policy key. Platform
    // activation and Owner republish both use these values so the two paths agree.
    public const string DriverReallocationRequestLimitKey = "DRIVER_REALLOCATION_REQUEST_LIMIT";
    public const int DriverReallocationLegacyMin = 0;
    public const int DriverReallocationLegacyMax = 5;

    public static Task AcquireAsync(ReservationDbContext db, CancellationToken ct) =>
        db.Database.IsRelational()
            ? db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({LockKey}, {LockNamespace}))", ct)
            : Task.CompletedTask;

    // Used by callers that already own a raw connection/transaction (for example the
    // two-connection PostgreSQL concurrency proof).
    public static async Task AcquireOnConnectionAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, @namespace))";
        var key = command.CreateParameter();
        key.ParameterName = "key";
        key.Value = LockKey;
        command.Parameters.Add(key);
        var ns = command.CreateParameter();
        ns.ParameterName = "namespace";
        ns.Value = LockNamespace;
        command.Parameters.Add(ns);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static bool IsRetryableConflict(Exception exception)
    {
        var postgres = exception as PostgresException
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres is
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
        };
    }

    public static TimeSpan RetryDelay(int attempt) => TimeSpan.FromMilliseconds(20 * (attempt + 1));
}
