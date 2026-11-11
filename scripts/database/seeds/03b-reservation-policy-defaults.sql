-- Explicitly authorized task-scoped seed; apply manually to an aligned Operations schema.
-- No prices, capacity, fictional site, Owner bounds or late-tolerance enablement are invented.
-- Replaying never overwrites a policy revision or resets its activation time.
INSERT INTO operational_policies (
    id, tenant_id, site_id, version,
    arrival_late_tolerance_enabled, arrival_late_tolerance_minutes,
    payment_hold_duration_minutes, min_reservation_duration_minutes, max_reservation_duration_hours,
    max_advance_booking_days, inter_booking_buffer_minutes, reservation_protection_window_hours,
    allocation_lead_time_minutes, facility_failure_refund_lead_minutes, occupancy_validity_timespan_hours,
    rapid_cancellation_count, rapid_cancellation_window_minutes, rapid_cancellation_restriction_minutes,
    daily_cancellation_limit, cancellation_timezone, override_bounds
) VALUES (
    '27300000-0000-4000-8000-000000000001', NULL, NULL, 1,
    NULL, 15,
    5, 15, 24,
    7, 0, 4,
    60, 15, 4,
    2, 15, 180,
    5, 'Asia/Ho_Chi_Minh', '{"ARRIVAL_LATE_TOLERANCE":{"minimum":15,"maximum":30}}'::jsonb
) ON CONFLICT (id) DO NOTHING;

-- A site revision must explicitly choose arrival_late_tolerance_enabled true or false.
-- Other nullable site values inherit; changed values require approved platform bounds in minutes
-- (COUNT/LIMIT entries are counts). No other numeric Owner bounds are approved/seeded here.
-- Keep referenced revisions and accepted reservation snapshots; this is not a cleanup/migration script.
