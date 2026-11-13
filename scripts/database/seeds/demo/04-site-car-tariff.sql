-- Run via scripts/seed-site-car-tariff.ps1 against PaymentDb only.
-- Site/Tenant/Owner variables are resolved from existing Facility/User records.
-- Reuse the baseline demo CAR price: day 20,000, night 30,000 VND/hour; grace 15 minutes.
-- Existing active tariffs and immutable policy revisions are never overwritten.
BEGIN;

SELECT NOT EXISTS (
    SELECT 1 FROM tariffs
    WHERE site_id = :'site_id'::uuid AND vehicle_type = 'CAR'
      AND is_active AND deleted_at IS NULL AND tenant_id <> :'tenant_id'::uuid
) AS ownership_valid \gset
\if :ownership_valid
\else
    \echo 'Active CAR tariff belongs to another tenant. Nothing changed.'
    ROLLBACK;
    \quit 1
\endif

SELECT EXISTS (
    SELECT 1 FROM tariffs
    WHERE id = 'c0000000-0000-4000-8000-000000000001'
      AND vehicle_type = 'CAR' AND is_active AND deleted_at IS NULL
) OR EXISTS (
    SELECT 1 FROM tariffs WHERE site_id = :'site_id'::uuid
      AND tenant_id = :'tenant_id'::uuid AND vehicle_type = 'CAR'
      AND is_active AND deleted_at IS NULL
) AS source_ready \gset
\if :source_ready
\else
    \echo 'Baseline demo CAR tariff is missing. Apply the existing PaymentDb baseline first.'
    ROLLBACK;
    \quit 1
\endif

INSERT INTO tariffs (id, tenant_id, site_id, name, vehicle_type,
    free_parking_grace_minutes, tariff_rules, is_active)
SELECT md5('smartpark:demo-car-tariff:' || :'site_id')::uuid,
    :'tenant_id'::uuid, :'site_id'::uuid, 'Demo CAR tariff for existing site', 'CAR',
    source.free_parking_grace_minutes, source.tariff_rules, TRUE
FROM tariffs source
WHERE source.id = 'c0000000-0000-4000-8000-000000000001'
  AND source.vehicle_type = 'CAR' AND source.is_active AND source.deleted_at IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM tariffs existing
    WHERE existing.tenant_id = :'tenant_id'::uuid AND existing.site_id = :'site_id'::uuid
      AND existing.vehicle_type = 'CAR' AND existing.is_active AND existing.deleted_at IS NULL
  )
ON CONFLICT (id) DO NOTHING;

INSERT INTO policy_revisions (id, policy_id, policy_type, revision_number, effective_from,
    scope_type, scope_id, policy_values, created_by)
SELECT md5('smartpark:demo-car-tariff-revision:' || :'site_id')::uuid,
    t.id, 'TARIFF', 1, CURRENT_TIMESTAMP, 'SITE', t.site_id, t.tariff_rules, :'owner_user_id'::uuid
FROM tariffs t
WHERE t.id = md5('smartpark:demo-car-tariff:' || :'site_id')::uuid
  AND t.tenant_id = :'tenant_id'::uuid AND t.site_id = :'site_id'::uuid
  AND t.vehicle_type = 'CAR' AND t.is_active AND t.deleted_at IS NULL
ON CONFLICT DO NOTHING;

SELECT EXISTS (
    SELECT 1 FROM tariffs WHERE site_id = :'site_id'::uuid AND tenant_id = :'tenant_id'::uuid
      AND vehicle_type = 'CAR' AND is_active AND deleted_at IS NULL
) AS tariff_ready \gset
\if :tariff_ready
\else
    \echo 'The deterministic seed ID already exists but is not active for this site. Nothing changed.'
    ROLLBACK;
    \quit 1
\endif

COMMIT;

SELECT 'Active CAR tariff: ' || id::text || ' | site: ' || site_id::text ||
    ' | grace: ' || free_parking_grace_minutes::text || ' minutes'
FROM tariffs WHERE site_id = :'site_id'::uuid AND tenant_id = :'tenant_id'::uuid
  AND vehicle_type = 'CAR' AND is_active AND deleted_at IS NULL;
