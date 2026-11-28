-- Local SPARK-286 read fixtures. The demo site script supplies the scoped IDs.
-- All bookings and visits are closed. Amounts, vehicle IDs and order references are synthetic.
BEGIN;

CREATE TEMP TABLE demo_history_cases ON COMMIT DROP AS
SELECT
    md5(fixture.key || ':request')::uuid AS request_id,
    md5(fixture.key || ':booking')::uuid AS reservation_id,
    md5(fixture.key || ':terms')::uuid AS terms_id,
    md5(fixture.key || ':vehicle')::uuid AS vehicle_id,
    md5(fixture.key || ':attempt')::uuid AS attempt_id,
    md5(fixture.key || ':confirmation')::uuid AS confirmation_id,
    md5(fixture.key || ':visit')::uuid AS session_id,
    md5(fixture.key || ':allocation')::uuid AS allocation_id,
    CASE WHEN scenario.has_order THEN md5(fixture.key || ':order')::uuid END AS order_id,
    scenario.status,
    scenario.plate,
    scenario.booked_amount,
    scenario.deposit_amount,
    scenario.requested_slot_id,
    scenario.allocated_slot_id,
    scenario.created_at,
    scenario.created_at + scenario.lead_time AS start_at,
    scenario.created_at + scenario.lead_time + INTERVAL '2 hours' AS end_at,
    CASE WHEN scenario.status <> 'EXPIRED' THEN scenario.created_at + INTERVAL '5 minutes' END AS confirmed_at
FROM (VALUES
    ('completed-paid', 'COMPLETED', '99A-28601', 40000, 20000, true,
        :'history_slot1'::uuid, :'history_slot2'::uuid, TIMESTAMPTZ '2025-01-15 02:00:00+00', INTERVAL '1 day'),
    ('completed-free', 'COMPLETED', '99A-28602', 0, 0, false,
        :'history_slot3'::uuid, :'history_slot3'::uuid, TIMESTAMPTZ '2025-01-15 02:00:00+00', INTERVAL '2 days'),
    ('expired', 'EXPIRED', '99A-28603', 40000, 20000, false,
        :'history_slot4'::uuid, NULL, TIMESTAMPTZ '2025-01-14 02:00:00+00', INTERVAL '1 day'),
    ('no-show', 'NO_SHOW', '99A-28604', 40000, 20000, true,
        :'history_slot5'::uuid, NULL, TIMESTAMPTZ '2025-01-13 02:00:00+00', INTERVAL '1 day')
) AS scenario(name, status, plate, booked_amount, deposit_amount, has_order,
    requested_slot_id, allocated_slot_id, created_at, lead_time)
CROSS JOIN LATERAL (
    SELECT 'SPARK-286:DEMO-HQ:' || :'driver_account_id'::uuid::text || ':' || scenario.name AS key
) AS fixture;

INSERT INTO reservation_requests (
    id, user_id, account_id, idempotency_key, operation, intent_json, fingerprint,
    state, admitted_at, overall_deadline, attempt_id,
    reservation_id, vehicle_binding_state, vehicle_binding_revision,
    vehicle_binding_checked_at, vehicle_plate
)
SELECT
    request_id, :'driver_user_id'::uuid, :'driver_account_id'::uuid, request_id,
    'CREATE_RESERVATION', jsonb_build_object('fixture', 'SPARK-286', 'reservationId', reservation_id),
    request_id::text, 'HOLD_CREATED', created_at, created_at + INTERVAL '2 minutes',
    attempt_id,
    reservation_id, 'ACTIVE', 1, created_at, replace(plate, '-', '')
FROM demo_history_cases
ON CONFLICT (id) DO NOTHING;

INSERT INTO reservation_terms (
    id, account_id, intent_fingerprint, version, valid_until, snapshot_json, accepted_request_id
)
SELECT terms_id, :'driver_account_id'::uuid, request_id::text, 'demo-spark-286',
    created_at + INTERVAL '15 minutes', '{"fixture":"SPARK-286"}'::jsonb, request_id
FROM demo_history_cases
ON CONFLICT (id) DO NOTHING;

INSERT INTO reservations (
    id, request_id, vehicle_id, terms_id, terms_version, selected_payment_method,
    booked_amount, deposit_amount, currency, payment_order_id, confirmation_state, confirmation_effect_id,
    confirmation_evaluated_at, tenant_id, site_id, account_id, target_slot_id,
    original_plate, normalized_plate, vehicle_type, expected_start_time, expected_end_time,
    confirmed_at, status, hold_expires_at, accepted_pricing_snapshot,
    accepted_operational_policy_snapshot, created_at
)
SELECT
    reservation_id, request_id, vehicle_id, terms_id, 'demo-spark-286', 'PAYOS',
    booked_amount, deposit_amount, 'VND', order_id,
    CASE WHEN confirmed_at IS NOT NULL THEN 'CONFIRMED' ELSE 'NO_ENTITLEMENT' END,
    CASE WHEN confirmed_at IS NOT NULL THEN confirmation_id END, confirmed_at,
    :'tenant_id'::uuid, :'site_id'::uuid, :'driver_account_id'::uuid, requested_slot_id,
    plate, replace(plate, '-', ''), 'CAR', start_at, end_at, confirmed_at, status,
    created_at + INTERVAL '15 minutes',
    jsonb_build_object('fixture', 'SPARK-286', 'bookedAmount', booked_amount,
        'depositAmount', deposit_amount, 'currency', 'VND'),
    '{"fixture":"SPARK-286"}'::jsonb, created_at
FROM demo_history_cases
ON CONFLICT (id) DO NOTHING;

INSERT INTO parking_sessions (
    id, reservation_id, tenant_id, site_id, account_id, current_slot_id,
    original_plate, normalized_plate, vehicle_type, entry_time, exit_time, session_status, created_at
)
SELECT session_id, reservation_id, :'tenant_id'::uuid, :'site_id'::uuid, :'driver_account_id'::uuid,
    allocated_slot_id, plate, replace(plate, '-', ''), 'CAR', start_at, end_at, 'COMPLETED', start_at
FROM demo_history_cases
WHERE allocated_slot_id IS NOT NULL
ON CONFLICT (id) DO NOTHING;

INSERT INTO slot_allocations (
    id, reservation_id, parking_session_id, tenant_id, site_id, slot_id,
    allocated_start_time, allocated_end_time, allocation_status, created_at
)
SELECT allocation_id, reservation_id, session_id, :'tenant_id'::uuid, :'site_id'::uuid,
    allocated_slot_id, start_at, end_at, 'RELEASED', start_at
FROM demo_history_cases
WHERE allocated_slot_id IS NOT NULL
ON CONFLICT (id) DO NOTHING;

COMMIT;
