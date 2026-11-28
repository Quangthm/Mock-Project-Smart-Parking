-- CompleteStructureEdit creates site state and adjusts only already-existing pools.
-- A fresh site needs no quota/pool, hold, operation receipt, or event for this snapshot.
SELECT pg_temp.require(current_database()='smartpark_operations' AND current_user='smartpark_operations','Unexpected OperationsDb identity');
LOCK TABLE structure_site_state IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE e_structure_site_state AS SELECT id AS site_id,tenant AS tenant_id,true AS active FROM site_input;
UPDATE run_context SET established=EXISTS(SELECT FROM structure_site_state p JOIN e_structure_site_state e USING(site_id));
SELECT pg_temp.require(NOT EXISTS(SELECT FROM structure_edit_holds h JOIN site_input s ON s.id=h.site_id)
    AND NOT EXISTS(SELECT FROM site_capacity_pools p JOIN site_input s ON s.id=p.site_id),
    'Existing structure edits or capacity pools require reconciliation');
SELECT pg_temp.sync('structure_site_state','site_id');
SELECT format('Operations persisted: %s active structure site states',count(*))
FROM structure_site_state p JOIN e_structure_site_state e USING(site_id)
WHERE p.active HAVING (SELECT mode='verify' FROM run_context);

-- This revision, separate from the A site-state anchor, commits with every B row.
LOCK TABLE operational_policies,reservation_quota_configs,parking_pass_plans,
    parking_pass_plan_revisions,subscribed_vehicles IN SHARE ROW EXCLUSIVE MODE;
UPDATE run_context SET established=EXISTS(SELECT FROM parking_pass_plan_revisions
    WHERE id=(SELECT planrev FROM commercial_input WHERE n='01'));

-- Coalesce adjacent compatible platform revisions. A gap or a change outside
-- approved bounds ends the usable window, even if an older revision was valid.
CREATE TEMP VIEW supported_platform AS
SELECT unnest(range_agg(tstzrange(effective_from,effective_until,'[)'))) AS span
FROM operational_policies
WHERE tenant_id IS NULL AND site_id IS NULL AND is_active AND version>0
    AND cancellation_timezone='Asia/Ho_Chi_Minh' AND inter_booking_buffer_minutes=0
    AND (payment_hold_duration_minutes,min_reservation_duration_minutes,max_reservation_duration_hours,
         max_advance_booking_days,reservation_protection_window_hours,allocation_lead_time_minutes,
         facility_failure_refund_lead_minutes,occupancy_validity_timespan_hours,rapid_cancellation_count,
         rapid_cancellation_window_minutes,rapid_cancellation_restriction_minutes,daily_cancellation_limit)
        =(5,15,24,7,4,60,15,4,2,15,180,5)
    AND arrival_late_tolerance_minutes BETWEEN 15 AND 30
    AND override_bounds->'ARRIVAL_LATE_TOLERANCE'='{"minimum":15,"maximum":30}'::jsonb;
SELECT pg_temp.require(EXISTS(SELECT FROM supported_platform WHERE span @> statement_timestamp()
    AND upper_inf(span)), 'An open, effective platform policy with the approved defaults and override bounds is required');
CREATE TEMP TABLE policy_window AS
SELECT greatest(lower(span),t-interval '90 days') AS begins,t
FROM supported_platform CROSS JOIN run_context WHERE span @> statement_timestamp();
SELECT format('Fixture policy history through T: start=%s, duration=%s. C must fit 75 reservations and 100 sessions within valid lifecycles.',
    begins,greatest(interval '0',t-begins)) FROM policy_window;
SELECT 'Checkpoint C blocked: platform has no history at or before T' FROM policy_window WHERE begins>=t;

SELECT pg_temp.require(NOT EXISTS(SELECT FROM operational_policies p JOIN commercial_input c
    ON p.site_id=c.site WHERE p.id<>c.sitepolicy AND (p.is_active OR p.version=1)),
    'A competing site policy or version exists');
CREATE TEMP TABLE e_operational_policies AS
SELECT sitepolicy AS id,tenant AS tenant_id,site AS site_id,1 AS version,begins AS effective_from,
    NULL::timestamptz AS effective_until,late IS NOT NULL AS arrival_late_tolerance_enabled,late AS arrival_late_tolerance_minutes,
    NULL::int AS payment_hold_duration_minutes,NULL::int AS min_reservation_duration_minutes,NULL::int AS max_reservation_duration_hours,
    NULL::int AS max_advance_booking_days,NULL::int AS inter_booking_buffer_minutes,NULL::int AS reservation_protection_window_hours,
    NULL::int AS allocation_lead_time_minutes,NULL::int AS facility_failure_refund_lead_minutes,NULL::int AS occupancy_validity_timespan_hours,
    NULL::int AS rapid_cancellation_count,NULL::int AS rapid_cancellation_window_minutes,NULL::int AS rapid_cancellation_restriction_minutes,
    NULL::int AS daily_cancellation_limit,NULL::text AS cancellation_timezone,NULL::jsonb AS override_bounds,
    NULL::int AS driver_reallocation_request_limit,NULL::jsonb AS parking_product_enabled,actor AS published_by,
    'MVP-SEED-V1'::text AS publish_note,true AS is_active,begins AS created_at,begins AS updated_at
FROM commercial_input CROSS JOIN policy_window;
SELECT pg_temp.sync('operational_policies','id',ARRAY['effective_from','created_at','updated_at']);

SELECT pg_temp.require(NOT EXISTS(SELECT FROM reservation_quota_configs q JOIN commercial_input c
    ON q.tenant_id=c.tenant AND q.site_id=c.site AND q.vehicle_type='CAR'
    WHERE q.id IS DISTINCT FROM c.quotaid AND (q.is_active OR c.quota IS NOT NULL)), 'CAR quota scope conflict');
CREATE TEMP TABLE e_reservation_quota_configs AS SELECT quotaid AS id,tenant AS tenant_id,site AS site_id,
    'CAR'::text AS vehicle_type,quota AS max_reservation_quota,true AS is_active,inception AS created_at,inception AS updated_at
FROM commercial_input WHERE quota IS NOT NULL;
SELECT pg_temp.sync('reservation_quota_configs','id',ARRAY['created_at','updated_at']);

SELECT pg_temp.require(NOT EXISTS(SELECT FROM parking_pass_plans p JOIN commercial_input c ON p.site_id=c.site
    WHERE p.product_type='MONTHLY' AND p.id<>c.plan) AND NOT EXISTS(SELECT FROM parking_pass_plan_revisions r
    JOIN commercial_input c ON r.plan_id=c.plan WHERE r.id<>c.planrev),'Existing monthly plan or later revision');
CREATE TEMP TABLE e_parking_pass_plans AS SELECT plan AS id,tenant AS tenant_id,site AS site_id,'MONTHLY'::text AS product_type,
    'WHEN_SPACE_AVAILABLE'::text AS entitlement_type,true AS is_enabled,'PUBLISHED'::text AS status,1 AS active_revision,
    NULL::int AS draft_revision,NULL::jsonb AS draft_payload,actor AS created_by,actor AS updated_by,
    inception AS created_at,inception AS updated_at FROM commercial_input;
SELECT pg_temp.sync('parking_pass_plans','id',ARRAY['created_at','updated_at']);
CREATE TEMP TABLE e_parking_pass_plan_revisions AS SELECT planrev AS id,plan AS plan_id,tenant AS tenant_id,site AS site_id,
    1 AS revision_number,'MONTHLY'::text AS product_type,NULL::uuid AS zone_id,'["CAR"]'::jsonb AS vehicle_types,
    '{"dayCovered":true,"nightCovered":true,"excludedZoneIds":[]}'::jsonb AS ordinary_coverage_rules,
    'Asia/Ho_Chi_Minh'::text AS contract_timezone,inception AS effective_from,NULL::timestamptz AS effective_to,
    'MVP-SEED-V1'::text AS publish_note,actor AS published_by,inception AS created_at FROM commercial_input;
SELECT pg_temp.sync('parking_pass_plan_revisions','id',ARRAY['effective_from','created_at']);

SELECT pg_temp.require(NOT EXISTS(SELECT FROM subscribed_vehicles v JOIN purchase_input p ON v.subscription_id<>p.subscription
    AND (v.transaction_id=p.cash OR (v.tenant_id=p.tenant AND v.site_id=p.site
    AND regexp_replace(upper(v.license_plate),'[^A-Z0-9]','','g')=p.canonical
    AND v.active_at<p.ends AND p.starts<v.expire_at))), 'Monthly checkout coverage overlaps or reuses a purchase transaction');
CREATE TEMP TABLE e_subscribed_vehicles AS SELECT subscription AS subscription_id,tenant AS tenant_id,site AS site_id,
    canonical AS license_plate,'CAR'::text AS vehicle_type,cash AS transaction_id,starts AS active_at,ends AS expire_at FROM purchase_input;
SELECT pg_temp.sync('subscribed_vehicles','subscription_id',ARRAY['active_at','expire_at']);
SELECT format('Operations verified: %s monthly coverage records at T',count(*))
FROM subscribed_vehicles v JOIN purchase_input p ON v.subscription_id=p.subscription CROSS JOIN run_context
WHERE v.active_at<=t AND t<v.expire_at HAVING (SELECT mode='verify' FROM run_context);

-- CHECKPOINT C
SELECT pg_temp.require(current_database()='smartpark_operations' AND current_user='smartpark_operations','Unexpected OperationsDb identity');
LOCK TABLE reservation_requests,reservations,reservation_cancellation_evidence,parking_sessions,slot_allocations,operational_policies
    IN SHARE ROW EXCLUSIVE MODE;
UPDATE run_context SET established=EXISTS(SELECT FROM reservation_requests
    WHERE id=(SELECT (j->>'workflow_anchor')::uuid FROM lifecycle_manifest));
SELECT pg_temp.require(NOT EXISTS(SELECT FROM reservations r JOIN site_input s ON s.id=r.site_id
    WHERE NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_array_elements(m.j->'reservations') e WHERE (e->>'id')::uuid=r.id))
    AND NOT EXISTS(SELECT FROM parking_sessions p JOIN site_input s ON s.id=p.site_id
    WHERE NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_array_elements(m.j->'parking_sessions') e WHERE (e->>'id')::uuid=p.id))
    AND NOT EXISTS(SELECT FROM slot_allocations a JOIN site_input s ON s.id=a.site_id
    WHERE NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_array_elements(m.j->'slot_allocations') e WHERE (e->>'id')::uuid=a.id)),
    'Nonfixture commitments, sessions or allocations on the fixture sites require reconciliation');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM
    jsonb_populate_recordset(NULL::parking_sessions,pg_temp.lifecycle_rows('parking_sessions')) e
    JOIN parking_sessions p ON p.normalized_plate=e.normalized_plate AND p.id<>e.id
    WHERE p.exit_time IS NULL AND e.exit_time IS NULL),
    'Fixture plate already has an open session');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM
    jsonb_populate_recordset(NULL::reservation_requests,pg_temp.lifecycle_rows('reservation_requests')) e
    JOIN reservation_requests r ON (r.account_id,r.operation,r.idempotency_key)=(e.account_id,e.operation,e.idempotency_key)
    WHERE r.id<>e.id), 'Competing reservation request idempotency key');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM jsonb_populate_recordset(NULL::reservations,pg_temp.lifecycle_rows('reservations')) e
    WHERE NOT EXISTS(SELECT FROM operational_policies p
        WHERE p.id=(e.accepted_operational_policy_snapshot#>>'{Platform,Id}')::uuid AND p.is_active AND p.site_id IS NULL
        AND p.version=(e.accepted_operational_policy_snapshot#>>'{Platform,Version}')::int
        AND p.effective_from<=e.created_at-interval '30 seconds'
        AND (p.effective_until IS NULL OR greatest(e.created_at,e.confirmed_at,e.cancelled_at)<p.effective_until))
    OR ((SELECT mode<>'check' FROM run_context) AND NOT current_setting('mvp.prior_timeline')::bool
        AND NOT EXISTS(SELECT FROM operational_policies p
        WHERE p.id=(e.accepted_operational_policy_snapshot#>>'{Site,Id}')::uuid AND p.is_active AND p.site_id=e.site_id
        AND p.tenant_id=e.tenant_id AND p.version=(e.accepted_operational_policy_snapshot#>>'{Site,Version}')::int
        AND p.effective_from<=e.created_at-interval '30 seconds'
        AND (p.effective_until IS NULL OR greatest(e.created_at,e.confirmed_at,e.cancelled_at)<p.effective_until)))),
    'C policy revision is outside its installed effective window');
SELECT pg_temp.sync_lifecycle('reservation_requests');
SELECT pg_temp.sync_lifecycle('reservations');
SELECT pg_temp.sync_lifecycle('reservation_cancellation_evidence');
SELECT pg_temp.sync_lifecycle('parking_sessions');
SELECT pg_temp.sync_lifecycle('slot_allocations');

