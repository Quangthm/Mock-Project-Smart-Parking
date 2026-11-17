using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationPolicyRepository(ReservationDbContext db, TimeProvider clock) : IReservationPolicyRepository
{
    // Every policy value an owner may override, with the factor that converts the stored column
    // unit into the minute unit used by platform override bounds.
    private static readonly (string Key, Func<OperationalPolicyRow, int?> Raw, int Scale)[] OwnerOverrideSlots =
    {
        ("RESERVATION_MIN_DURATION", row => row.MinReservationDurationMinutes, 1),
        ("RESERVATION_MAX_DURATION", row => row.MaxReservationDurationHours, 60),
        ("MAX_ADVANCE_BOOKING", row => row.MaxAdvanceBookingDays, 1440),
        ("INTER_BOOKING_BUFFER", row => row.InterBookingBufferMinutes, 1),
        ("PAYMENT_HOLD_DURATION", row => row.PaymentHoldDurationMinutes, 1),
        ("RESERVATION_PROTECTION_WINDOW", row => row.ReservationProtectionWindowHours, 60),
        ("ALLOCATION_LEAD_TIME", row => row.AllocationLeadTimeMinutes, 1),
        ("FACILITY_FAILURE_REFUND_LEAD_TIME", row => row.FacilityFailureRefundLeadMinutes, 1),
        ("OCCUPANCY_VALIDITY_TIMESPAN", row => row.OccupancyValidityTimespanHours, 60),
        ("ARRIVAL_LATE_TOLERANCE", row => row.ArrivalLateToleranceMinutes, 1),
        ("DRIVER_REALLOCATION_REQUEST_LIMIT", row => row.DriverReallocationRequestLimit, 1)
    };

    public async Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        var rows = await db.Set<OperationalPolicyRow>().AsNoTracking().Where(x => x.IsActive && x.EffectiveFrom <= at &&
            (x.EffectiveUntil == null || at < x.EffectiveUntil) &&
            (x.TenantId == null && x.SiteId == null || x.TenantId == tenantId && x.SiteId == siteId)).ToListAsync(ct);
        return rows.Select(ToRevision).ToArray();
    }

    public async Task<PlatformPolicyCatalogueDto> GetCatalogueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var row = await db.Set<OperationalPolicyRow>().AsNoTracking().SingleOrDefaultAsync(x => x.IsActive &&
            x.TenantId == null && x.SiteId == null && x.EffectiveFrom <= now &&
            (x.EffectiveUntil == null || now < x.EffectiveUntil), ct)
            ?? throw new ReservationPolicyUnavailableException("No active platform policy revision is available.");
        var revision = ToRevision(row);
        var policies = PlatformPolicyCatalogueMetadata.All.Select(item =>
        {
            var value = revision.Values.TryGetValue(item.Key, out var activeVal) ? activeVal : item.PlatformDefault;
            PolicyBoundDto? bound = null;
            // An active revision always exists here. Its OverrideBounds is authoritative: a key the
            // Admin omitted has been explicitly removed and must surface as null, never fabricated
            // from metadata defaults.
            if (item.IsOverridableByOwner && revision.OverrideBounds.TryGetValue(item.Key, out var activeBound))
            {
                bound = new PolicyBoundDto(activeBound.Minimum, activeBound.Maximum);
            }
            return new PolicyCatalogueItemDto(item.Key, item.Name, item.Description, item.DataType, item.Unit,
                value, item.IsOverridableByOwner, bound);
        }).ToArray();
        return new PlatformPolicyCatalogueDto(revision.Id, revision.Version, row.EffectiveFrom,
            revision.CancellationTimezone ?? "Asia/Ho_Chi_Minh", policies);
    }

    public async Task<ReservationPolicyRevision?> GetActivePlatformRevisionAsync(DateTimeOffset at,
        CancellationToken ct)
    {
        var row = await db.Set<OperationalPolicyRow>().AsNoTracking().SingleOrDefaultAsync(x => x.IsActive &&
            x.TenantId == null && x.SiteId == null && x.EffectiveFrom <= at &&
            (x.EffectiveUntil == null || at < x.EffectiveUntil), ct);
        return row is null ? null : ToRevision(row);
    }

    public async Task<EffectivePolicyLookup> GetEffectivePolicyAsync(Guid? tenantId, Guid? siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        if (tenantId is null || siteId is null)
            return new EffectivePolicyLookup(await GetActivePlatformRevisionAsync(at, ct), null, false);

        var mismatch = await db.Set<OperationalPolicyRow>().AsNoTracking()
            .AnyAsync(x => x.SiteId == siteId && x.TenantId != tenantId, ct);
        var platform = await GetActivePlatformRevisionAsync(at, ct);
        var siteRows = await db.Set<OperationalPolicyRow>().AsNoTracking().Where(x => x.IsActive &&
            x.TenantId == tenantId && x.SiteId == siteId && x.EffectiveFrom <= at &&
            (x.EffectiveUntil == null || at < x.EffectiveUntil)).ToListAsync(ct);
        if (siteRows.Count > 1)
            throw new ReservationPolicyUnavailableException("Active site policy revisions are ambiguous.");
        return new EffectivePolicyLookup(platform, siteRows.Count == 0 ? null : ToRevision(siteRows[0]), mismatch);
    }

    public async Task<PagedResult<PolicyRevisionListItemDto>> GetRevisionsAsync(string status, string scope,
        Guid? tenantId, Guid? siteId, int page, int pageSize, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Set<OperationalPolicyRow>().AsNoTracking();
        if (scope == "PLATFORM")
            query = query.Where(x => x.TenantId == null && x.SiteId == null);
        else if (scope == "SITE")
            query = query.Where(x => x.TenantId != null && x.SiteId != null);
        if (tenantId is not null) query = query.Where(x => x.TenantId == tenantId);
        if (siteId is not null) query = query.Where(x => x.SiteId == siteId);

        // DRAFT is a platform-only lifecycle state. Owner cancellation only flips is_active for a
        // site row (see LotPolicyRepository), so an inactive site row is a superseded/cancelled
        // revision, never a draft. Keep every predicate aligned with ToListItem below.
        if (status == "DRAFT")
            query = query.Where(x => x.TenantId == null && x.SiteId == null && !x.IsActive &&
                                     (x.EffectiveUntil == null || x.EffectiveUntil > now));
        else if (status == "SCHEDULED")
            query = query.Where(x => x.IsActive && x.EffectiveFrom > now && (x.EffectiveUntil == null || x.EffectiveUntil > now));
        else if (status == "CURRENT")
            query = query.Where(x => x.IsActive && x.EffectiveFrom <= now && (x.EffectiveUntil == null || now < x.EffectiveUntil));
        else if (status == "SUPERSEDED")
            query = query.Where(x => (x.EffectiveUntil != null && x.EffectiveUntil <= now) ||
                                     (x.SiteId != null && !x.IsActive));

        var totalCount = await query.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var rows = await query
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.EffectiveFrom)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(row => ToListItem(row, now)).ToArray();
        return new PagedResult<PolicyRevisionListItemDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<PolicyRevisionListItemDto?> GetRevisionByIdAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<OperationalPolicyRow>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToListItem(row, clock.GetUtcNow());
    }

    public async Task<int> GetNextPlatformVersionAsync(CancellationToken ct)
    {
        var maximum = await db.Set<OperationalPolicyRow>().AsNoTracking()
            .Where(x => x.TenantId == null && x.SiteId == null)
            .Select(x => (int?)x.Version).MaxAsync(ct);
        return (maximum ?? 0) + 1;
    }

    public async Task CreateDraftAsync(OperationalPolicyRow draft, CancellationToken ct)
    {
        db.Set<OperationalPolicyRow>().Add(draft);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DateTimeOffset> UpdateDraftOptimisticAsync(Guid id, OperationalPolicyRow updated,
        DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE operational_policies SET
                    payment_hold_duration_minutes = @PaymentHoldDurationMinutes,
                    min_reservation_duration_minutes = @MinReservationDurationMinutes,
                    max_reservation_duration_hours = @MaxReservationDurationHours,
                    max_advance_booking_days = @MaxAdvanceBookingDays,
                    inter_booking_buffer_minutes = @InterBookingBufferMinutes,
                    reservation_protection_window_hours = @ReservationProtectionWindowHours,
                    allocation_lead_time_minutes = @AllocationLeadTimeMinutes,
                    facility_failure_refund_lead_minutes = @FacilityFailureRefundLeadMinutes,
                    occupancy_validity_timespan_hours = @OccupancyValidityTimespanHours,
                    arrival_late_tolerance_minutes = @ArrivalLateToleranceMinutes,
                    rapid_cancellation_count = @RapidCancellationCount,
                    rapid_cancellation_window_minutes = @RapidCancellationWindowMinutes,
                    rapid_cancellation_restriction_minutes = @RapidCancellationRestrictionMinutes,
                    daily_cancellation_limit = @DailyCancellationLimit,
                    override_bounds = CAST(@overrideBounds AS jsonb),
                    updated_at = GREATEST(CURRENT_TIMESTAMP, @expectedUpdatedAt + interval '1 microsecond')
                WHERE id = @id
                  AND tenant_id IS NULL
                  AND site_id IS NULL
                  AND is_active = false
                  AND updated_at = @expectedUpdatedAt
                RETURNING updated_at;
                """;
            AddParameter(command, "PaymentHoldDurationMinutes", updated.PaymentHoldDurationMinutes);
            AddParameter(command, "MinReservationDurationMinutes", updated.MinReservationDurationMinutes);
            AddParameter(command, "MaxReservationDurationHours", updated.MaxReservationDurationHours);
            AddParameter(command, "MaxAdvanceBookingDays", updated.MaxAdvanceBookingDays);
            AddParameter(command, "InterBookingBufferMinutes", updated.InterBookingBufferMinutes);
            AddParameter(command, "ReservationProtectionWindowHours", updated.ReservationProtectionWindowHours);
            AddParameter(command, "AllocationLeadTimeMinutes", updated.AllocationLeadTimeMinutes);
            AddParameter(command, "FacilityFailureRefundLeadMinutes", updated.FacilityFailureRefundLeadMinutes);
            AddParameter(command, "OccupancyValidityTimespanHours", updated.OccupancyValidityTimespanHours);
            AddParameter(command, "ArrivalLateToleranceMinutes", updated.ArrivalLateToleranceMinutes);
            AddParameter(command, "RapidCancellationCount", updated.RapidCancellationCount);
            AddParameter(command, "RapidCancellationWindowMinutes", updated.RapidCancellationWindowMinutes);
            AddParameter(command, "RapidCancellationRestrictionMinutes", updated.RapidCancellationRestrictionMinutes);
            AddParameter(command, "DailyCancellationLimit", updated.DailyCancellationLimit);
            AddParameter(command, "overrideBounds", updated.OverrideBounds);
            AddParameter(command, "expectedUpdatedAt", expectedUpdatedAt);
            AddParameter(command, "id", id);

            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                    return reader.GetFieldValue<DateTimeOffset>(0);
            }

            await ClassifyZeroRowsAsync(connection, id, ct);
            throw new InvalidOperationException("Concurrency classification did not produce an exception.");
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    public async Task<PolicyActivationOutcome> ActivateRevisionAsync(Guid id, DateTimeOffset effectiveFrom,
        CancellationToken ct)
    {
        // A serialization failure or deadlock always rolls the whole transaction back, so the
        // database is never left partially written. Retry a bounded number of times; if the
        // conflict persists, surface it as a 409 instead of a raw provider exception.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ActivateRevisionTransactionalAsync(id, effectiveFrom, ct);
            }
            catch (Exception exception) when (PolicyCompatibility.IsRetryableConflict(exception))
            {
                if (attempt >= PolicyCompatibility.MaxConflictRetries - 1)
                {
                    throw new ResourceVersionConflictException(
                        "The policy activation was aborted by a conflicting concurrent write. Reload and retry.");
                }

                db.ChangeTracker.Clear();
                await Task.Delay(PolicyCompatibility.RetryDelay(attempt), ct);
            }
        }
    }

    private async Task<PolicyActivationOutcome> ActivateRevisionTransactionalAsync(Guid id, DateTimeOffset effectiveFrom,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Shared with Owner publish: acquire before reading any policy rows so a concurrent
        // site publication cannot validate against state this activation is about to replace.
        await PolicyCompatibility.AcquireAsync(db, ct);

        var target = (await db.Set<OperationalPolicyRow>()
                .FromSqlInterpolated($"SELECT * FROM operational_policies WHERE id = {id} FOR UPDATE")
                .ToListAsync(ct))
            .SingleOrDefault();
        if (target is null)
        {
            await transaction.RollbackAsync(ct);
            throw new PolicyNotFoundException("The requested policy revision does not exist.");
        }

        if (target.TenantId is not null || target.SiteId is not null)
        {
            await transaction.RollbackAsync(ct);
            throw new ForbiddenScopeException("Platform administrators may only activate platform-scope policy revisions.");
        }

        if (target.IsActive)
        {
            await transaction.RollbackAsync(ct);
            throw new CannotActivateNonDraftException("Only draft policy revisions can be activated.");
        }

        var active = await db.Set<OperationalPolicyRow>()
            .FromSqlInterpolated(
                $"SELECT * FROM operational_policies WHERE tenant_id IS NULL AND site_id IS NULL AND is_active = true FOR UPDATE")
            .ToListAsync(ct);

        var collision = active.FirstOrDefault(row => row.Id != id && row.EffectiveFrom == effectiveFrom);
        if (collision is not null)
        {
            await transaction.RollbackAsync(ct);
            throw new OverlappingPolicyIntervalException(
                $"Activation rejected: An active or scheduled policy revision (Version {collision.Version}) already starts at the exact timestamp {effectiveFrom:O}.",
                "effectiveFrom");
        }

        var predecessor = active.Where(row => row.Id != id && row.EffectiveFrom < effectiveFrom &&
                (row.EffectiveUntil is null || row.EffectiveUntil > effectiveFrom))
            .OrderByDescending(row => row.EffectiveFrom)
            .FirstOrDefault();
        var successor = active.Where(row => row.Id != id && row.EffectiveFrom > effectiveFrom)
            .OrderBy(row => row.EffectiveFrom)
            .FirstOrDefault();

        // Admin activation must remain compatible with every already-published site policy whose
        // effective interval intersects the new platform interval. Runs inside the activation
        // transaction; a conflict rolls the whole activation back.
        await ValidateIntersectingSiteCompatibilityAsync(target, effectiveFrom, successor?.EffectiveFrom, ct);

        try
        {
            if (predecessor is not null)
            {
                predecessor.EffectiveUntil = effectiveFrom;
                predecessor.UpdatedAt = now;
                // Close the predecessor interval first so the non-deferrable GiST
                // exclusion constraint never observes a transient overlap.
                await db.SaveChangesAsync(ct);
            }

            target.EffectiveFrom = effectiveFrom;
            target.EffectiveUntil = successor?.EffectiveFrom;
            target.IsActive = true;
            target.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (ResolvePostgres(exception) is
            { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: "operational_policy_effective_scope" })
        {
            await transaction.RollbackAsync(ct);
            throw new OverlappingPolicyIntervalException(
                "Activation rejected: The specified effective interval overlaps with another active or scheduled policy revision.");
        }

        return new PolicyActivationOutcome(target, predecessor);
    }

    /// <summary>
    /// Validates that activating <paramref name="target"/> (a platform revision) over the interval
    /// [<paramref name="effectiveFrom"/>, <paramref name="intervalEnd"/> or +inf) does not invalidate
    /// any active site policy whose effective interval intersects it. A site value that diverges from
    /// the new platform value must remain within the target's override bounds; if the key was removed
    /// from those bounds, activation is rejected.
    /// </summary>
    public async Task ValidateIntersectingSiteCompatibilityAsync(OperationalPolicyRow target,
        DateTimeOffset effectiveFrom, DateTimeOffset? intervalEnd, CancellationToken ct)
    {
        var bounds = ToDomainBounds(target.OverrideBounds);
        var siteRows = await db.Set<OperationalPolicyRow>().AsNoTracking()
            .Where(x => x.IsActive && x.TenantId != null && x.SiteId != null)
            .Where(x => (intervalEnd == null || x.EffectiveFrom < intervalEnd) &&
                        (x.EffectiveUntil == null || effectiveFrom < x.EffectiveUntil))
            .ToListAsync(ct);

        foreach (var site in siteRows)
        {
            foreach (var (key, raw, scale) in OwnerOverrideSlots)
            {
                if (raw(site) is not { } siteRaw) continue;
                var siteMinutes = (long)siteRaw * scale;
                if (raw(target) is { } targetRaw && (long)targetRaw * scale == siteMinutes)
                    continue; // Matches the new platform value: not an active override.

                if (bounds.TryGetValue(key, out var bound) &&
                    siteMinutes >= bound.Minimum && siteMinutes <= bound.Maximum)
                    continue;

                if (!bounds.ContainsKey(key) && key == PolicyCompatibility.DriverReallocationRequestLimitKey &&
                    siteMinutes >= PolicyCompatibility.DriverReallocationLegacyMin &&
                    siteMinutes <= PolicyCompatibility.DriverReallocationLegacyMax)
                {
                    // Established legacy bound for DRIVER_REALLOCATION_REQUEST_LIMIT is [0..5].
                    // Owner republish applies the same range through PolicyCompatibility.
                    continue;
                }

                var reason = bounds.TryGetValue(key, out var resolved)
                    ? $"override {siteMinutes} minutes is outside the new platform bounds {resolved.Minimum}..{resolved.Maximum} minutes"
                    : "the new platform bounds no longer permit an override for this key";
                throw new PolicyBoundsConflictException(site.SiteId!.Value, key,
                    $"Activation rejected: site policy {site.SiteId} conflicts on {key} ({reason}).");
            }

            // Validate composed post-inheritance invariants
            var siteMinMinutes = site.MinReservationDurationMinutes ?? target.MinReservationDurationMinutes;
            var siteMaxMinutes = (long?)(site.MaxReservationDurationHours ?? target.MaxReservationDurationHours) * 60;
            if (siteMinMinutes is { } min && siteMaxMinutes is { } max && max < min)
            {
                throw new PolicyBoundsConflictException(site.SiteId!.Value, "RESERVATION_MAX_DURATION",
                    $"Activation rejected: site policy {site.SiteId} composed maximum reservation duration ({max} minutes) is less than the new platform minimum ({min} minutes).");
            }
        }
    }

    private async Task ClassifyZeroRowsAsync(DbConnection connection, Guid id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT tenant_id, site_id, is_active, updated_at FROM operational_policies WHERE id = @id;";
        AddParameter(command, "id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new PolicyNotFoundException("The requested policy revision does not exist.");
        var hasTenant = !reader.IsDBNull(0);
        var hasSite = !reader.IsDBNull(1);
        var isActive = reader.GetBoolean(2);
        if (hasTenant || hasSite)
            throw new ForbiddenScopeException("Platform administrators may only modify platform-scope policy drafts.");
        if (isActive)
            throw new CannotModifyActivePolicyException(
                "This policy revision has been activated concurrently by another administrator and cannot be modified.");
        throw new ResourceVersionConflictException(
            "The draft was modified concurrently by another session. Please reload and review the current draft before saving.");
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static PostgresException? ResolvePostgres(Exception exception) =>
        exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;

    private static ReservationPolicyRevision ToRevision(OperationalPolicyRow row)
    {
        var values = new Dictionary<string, int>();
        void Add(string key, int? value, int scale = 1)
        {
            if (value is not { } v) return;
            var scaled = (long)v * scale;
            if (scaled > int.MaxValue || scaled < 0)
                throw new ReservationPolicyUnavailableException($"Policy {key} value {v} exceeds the supported range.");
            values.Add(key, (int)scaled);
        }
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
        var bounds = ToDomainBounds(row.OverrideBounds);
        return new ReservationPolicyRevision(row.Id, row.Version, row.TenantId, row.SiteId, values, bounds,
            row.ArrivalLateToleranceEnabled, row.CancellationTimezone);
    }

    private static PolicyRevisionListItemDto ToListItem(OperationalPolicyRow row, DateTimeOffset now)
    {
        // Owner lifecycle vocabulary (v1.6) defines DRAFT/SCHEDULED/CURRENT/SUPERSEDED only, with no
        // CANCELLED state. An inactive SITE row (owner cancellation flips is_active without setting
        // effective_until) is classified as SUPERSEDED, never DRAFT.
        var status = (row.EffectiveUntil is { } until && until <= now) ? "SUPERSEDED"
            : !row.IsActive ? (row.SiteId is not null ? "SUPERSEDED" : "DRAFT")
            : row.EffectiveFrom > now ? "SCHEDULED"
            : "CURRENT";
        var scope = row.TenantId is null && row.SiteId is null ? "PLATFORM" : "SITE";
        var bounds = ToDomainBounds(row.OverrideBounds)
            .ToDictionary(pair => pair.Key, pair => new PolicyBoundDto(pair.Value.Minimum, pair.Value.Maximum));
        return new PolicyRevisionListItemDto
        {
            Id = row.Id,
            Scope = scope,
            TenantId = row.TenantId,
            SiteId = row.SiteId,
            Version = row.Version,
            Status = status,
            IsActive = row.IsActive,
            EffectiveFrom = row.EffectiveFrom,
            EffectiveUntil = row.EffectiveUntil,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            PaymentHoldDurationMinutes = row.PaymentHoldDurationMinutes,
            MinReservationDurationMinutes = row.MinReservationDurationMinutes,
            MaxReservationDurationHours = row.MaxReservationDurationHours,
            MaxAdvanceBookingDays = row.MaxAdvanceBookingDays,
            InterBookingBufferMinutes = row.InterBookingBufferMinutes,
            ReservationProtectionWindowHours = row.ReservationProtectionWindowHours,
            AllocationLeadTimeMinutes = row.AllocationLeadTimeMinutes,
            FacilityFailureRefundLeadMinutes = row.FacilityFailureRefundLeadMinutes,
            OccupancyValidityTimespanHours = row.OccupancyValidityTimespanHours,
            ArrivalLateToleranceMinutes = row.ArrivalLateToleranceMinutes,
            ArrivalLateToleranceEnabled = row.ArrivalLateToleranceEnabled,
            RapidCancellationCount = row.RapidCancellationCount,
            RapidCancellationWindowMinutes = row.RapidCancellationWindowMinutes,
            RapidCancellationRestrictionMinutes = row.RapidCancellationRestrictionMinutes,
            DailyCancellationLimit = row.DailyCancellationLimit,
            CancellationTimezone = row.CancellationTimezone,
            OverrideBounds = bounds
        };
    }

    private static Dictionary<string, PolicyBound> ToDomainBounds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, PolicyBound>();
        return JsonSerializer.Deserialize<Dictionary<string, PolicyBound>>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ReservationPolicyUnavailableException("Policy bounds are invalid.");
    }
}