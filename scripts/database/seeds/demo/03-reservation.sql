-- Demo reservation/operations seed for ReservationDb.
-- site_capacity_pools has no natural unique constraint, so use explicit existence checks.
-- Match the Facility seed's 48 car spaces and 2 motorcycle capacity units.
-- Reservation quotas are intentionally lower for cars. Preserve existing operational configurations.
BEGIN;

INSERT INTO site_capacity_pools (
    tenant_id, site_id, spatial_unit_id, vehicle_type,
    total_capacity, max_reservation_quota
)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    NULL,
    'CAR',
    48,
    4
WHERE NOT EXISTS (
    SELECT 1
    FROM site_capacity_pools
    WHERE tenant_id = :'tenant_id'::uuid
      AND site_id = :'site_id'::uuid
      AND spatial_unit_id IS NULL
      AND vehicle_type = 'CAR'
);

INSERT INTO site_capacity_pools (
    tenant_id, site_id, spatial_unit_id, vehicle_type,
    total_capacity, max_reservation_quota
)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    NULL,
    'MOTORCYCLE',
    2,
    2
WHERE NOT EXISTS (
    SELECT 1
    FROM site_capacity_pools
    WHERE tenant_id = :'tenant_id'::uuid
      AND site_id = :'site_id'::uuid
      AND spatial_unit_id IS NULL
      AND vehicle_type = 'MOTORCYCLE'
);

INSERT INTO operational_capacity_configs (
    tenant_id, site_id, spatial_unit_id, vehicle_type,
    total_physical_capacity, max_reservation_quota, is_active
)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    NULL,
    'CAR',
    48,
    4,
    TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM operational_capacity_configs
    WHERE tenant_id = :'tenant_id'::uuid
      AND site_id = :'site_id'::uuid
      AND spatial_unit_id IS NULL
      AND vehicle_type = 'CAR'
);

INSERT INTO operational_capacity_configs (
    tenant_id, site_id, spatial_unit_id, vehicle_type,
    total_physical_capacity, max_reservation_quota, is_active
)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    NULL,
    'MOTORCYCLE',
    2,
    2,
    TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM operational_capacity_configs
    WHERE tenant_id = :'tenant_id'::uuid
      AND site_id = :'site_id'::uuid
      AND spatial_unit_id IS NULL
      AND vehicle_type = 'MOTORCYCLE'
);

UPDATE operational_policies
SET is_active = TRUE,
    updated_at = CURRENT_TIMESTAMP
WHERE tenant_id = :'tenant_id'::uuid
  AND site_id = :'site_id'::uuid;

INSERT INTO operational_policies (
    tenant_id, site_id,
    arrival_late_tolerance_minutes,
    payment_hold_duration_minutes,
    min_reservation_duration_minutes,
    max_reservation_duration_hours,
    reservation_protection_window_hours,
    allocation_lead_time_minutes,
    occupancy_validity_timespan_hours,
    is_active
)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    15,
    15,
    15,
    24,
    4,
    60,
    4,
    TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM operational_policies
    WHERE tenant_id = :'tenant_id'::uuid
      AND site_id = :'site_id'::uuid
);

COMMIT;
