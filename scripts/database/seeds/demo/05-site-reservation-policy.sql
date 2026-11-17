-- Run only through seed-site-reservation-policy.ps1. The runner opens the transaction
-- and conditionally loads the existing approved platform seed before this file.
-- New site revisions inherit all numerical values. Existing snapshots retain old IDs.
SELECT count(*) = 1 AND bool_and(
    cancellation_timezone = 'Asia/Ho_Chi_Minh'
    AND arrival_late_tolerance_minutes BETWEEN 15 AND 30
    AND payment_hold_duration_minutes > 0 AND min_reservation_duration_minutes > 0
    AND max_reservation_duration_hours * 60::bigint >= min_reservation_duration_minutes
    AND max_advance_booking_days >= 0 AND inter_booking_buffer_minutes >= 0
    AND reservation_protection_window_hours >= 0 AND allocation_lead_time_minutes >= 0
    AND facility_failure_refund_lead_minutes >= 0 AND occupancy_validity_timespan_hours >= 0
    AND rapid_cancellation_count > 0 AND rapid_cancellation_window_minutes >= 0
    AND rapid_cancellation_restriction_minutes >= 0 AND daily_cancellation_limit > 0
) AS platform_valid
FROM operational_policies WHERE tenant_id IS NULL AND site_id IS NULL AND is_active
  AND effective_from <= CURRENT_TIMESTAMP AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until)
\gset
\if :platform_valid
\else
    \echo 'Platform policy is absent, ambiguous or invalid. No policy changes committed.'
    ROLLBACK;
    \quit 1
\endif

-- Do not collide with or replace a separately scheduled future Owner policy.
SELECT NOT EXISTS (SELECT 1 FROM operational_policies
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid AND is_active
      AND effective_from >= CURRENT_TIMESTAMP) AS schedule_clear \gset
\if :schedule_clear
\else
    \echo 'A current-transaction or future site policy exists. Review it before reseeding.'
    ROLLBACK;
    \quit 1
\endif

SELECT EXISTS (SELECT 1 FROM operational_policies
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid AND is_active
      AND effective_from <= CURRENT_TIMESTAMP AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until)
      AND arrival_late_tolerance_enabled = :'late_tolerance'::boolean
      AND arrival_late_tolerance_minutes IS NULL AND payment_hold_duration_minutes IS NULL
      AND min_reservation_duration_minutes IS NULL AND max_reservation_duration_hours IS NULL
      AND max_advance_booking_days IS NULL AND inter_booking_buffer_minutes IS NULL
      AND reservation_protection_window_hours IS NULL AND allocation_lead_time_minutes IS NULL
      AND facility_failure_refund_lead_minutes IS NULL AND occupancy_validity_timespan_hours IS NULL
      AND rapid_cancellation_count IS NULL AND rapid_cancellation_window_minutes IS NULL
      AND rapid_cancellation_restriction_minutes IS NULL AND daily_cancellation_limit IS NULL
      AND cancellation_timezone IS NULL AND override_bounds IS NULL) AS site_ready \gset
\if :site_ready
    \echo 'Site already inherits platform policy with the requested late-tolerance choice. No changes.'
\else
    SELECT NOT EXISTS (SELECT 1 FROM operational_policies
        WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid AND is_active
          AND effective_from <= CURRENT_TIMESTAMP AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until))
        OR :'replace_existing'::boolean AS may_write \gset
    \if :may_write
    \else
        \echo 'Existing site policy needs review. Use -ReplaceExisting only to explicitly replace it with platform inheritance.'
        ROLLBACK;
        \quit 1
    \endif

    UPDATE operational_policies SET effective_until = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP
    WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid AND is_active
      AND effective_from < CURRENT_TIMESTAMP AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until);

    INSERT INTO operational_policies (
        tenant_id, site_id, version, arrival_late_tolerance_enabled,
        arrival_late_tolerance_minutes, payment_hold_duration_minutes,
        min_reservation_duration_minutes, max_reservation_duration_hours,
        max_advance_booking_days, inter_booking_buffer_minutes,
        reservation_protection_window_hours, allocation_lead_time_minutes,
        facility_failure_refund_lead_minutes, occupancy_validity_timespan_hours,
        rapid_cancellation_count, rapid_cancellation_window_minutes,
        rapid_cancellation_restriction_minutes, daily_cancellation_limit,
        cancellation_timezone, override_bounds)
    SELECT :'tenant_id'::uuid, :'site_id'::uuid, COALESCE(MAX(version), 0) + 1, :'late_tolerance'::boolean,
        NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
        NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
    FROM operational_policies WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid;
    \echo 'Added a new site policy revision inheriting platform values. Previous revisions retained.'
\endif

COMMIT;
SELECT 'Effective policy: ' || id::text || ' | version: ' || version::text ||
    ' | late tolerance enabled: ' || arrival_late_tolerance_enabled::text
FROM operational_policies WHERE tenant_id = :'tenant_id'::uuid AND site_id = :'site_id'::uuid
  AND is_active AND effective_from <= CURRENT_TIMESTAMP
  AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until);
