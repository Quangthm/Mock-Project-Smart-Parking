-- Demo facility seed for ParkingDb.
-- Creates one tenant copy, one site, a floor + zone, 48 slots and static capacity.
BEGIN;

INSERT INTO tenants (id, code, name, contact_email, contact_phone, status)
VALUES (
    :'tenant_id'::uuid,
    'DEMO-OWNER',
    'SmartPark Demo Owner',
    :'owner_email',
    '0900000001',
    'ACTIVE'
)
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name,
    contact_email = EXCLUDED.contact_email,
    contact_phone = EXCLUDED.contact_phone,
    status = 'ACTIVE',
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO parking_sites (
    id, tenant_id, site_code, name, address, latitude, longitude,
    total_physical_capacity, status
)
VALUES (
    :'site_id'::uuid,
    :'tenant_id'::uuid,
    'DEMO-HQ',
    'SmartPark Demo Site',
    '1 Demo Street',
    10.77690000,
    106.70090000,
    48,
    'ACTIVE'
)
ON CONFLICT (id) DO UPDATE
SET tenant_id = EXCLUDED.tenant_id,
    site_code = EXCLUDED.site_code,
    name = EXCLUDED.name,
    address = EXCLUDED.address,
    latitude = EXCLUDED.latitude,
    longitude = EXCLUDED.longitude,
    total_physical_capacity = EXCLUDED.total_physical_capacity,
    status = 'ACTIVE',
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO spatial_units (
    id, tenant_id, site_id, parent_id, path, unit_type, name, max_capacity
)
VALUES (
    :'floor_id'::uuid,
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    NULL,
    'F1',
    'FLOOR',
    'Floor 1',
    48
)
ON CONFLICT (id) DO UPDATE
SET tenant_id = EXCLUDED.tenant_id,
    site_id = EXCLUDED.site_id,
    parent_id = EXCLUDED.parent_id,
    path = EXCLUDED.path,
    unit_type = EXCLUDED.unit_type,
    name = EXCLUDED.name,
    max_capacity = EXCLUDED.max_capacity,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO spatial_units (
    id, tenant_id, site_id, parent_id, path, unit_type, name, max_capacity
)
VALUES (
    :'zone_id'::uuid,
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    :'floor_id'::uuid,
    'F1/Z1',
    'ZONE',
    'Zone A',
    48
)
ON CONFLICT (id) DO UPDATE
SET tenant_id = EXCLUDED.tenant_id,
    site_id = EXCLUDED.site_id,
    parent_id = EXCLUDED.parent_id,
    path = EXCLUDED.path,
    unit_type = EXCLUDED.unit_type,
    name = EXCLUDED.name,
    max_capacity = EXCLUDED.max_capacity,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO spatial_unit_capacities (
    tenant_id, site_id, spatial_unit_id, vehicle_type, max_capacity
)
VALUES
    (:'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR', 48),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'MOTORCYCLE', 2)
ON CONFLICT (spatial_unit_id, vehicle_type) DO UPDATE
SET max_capacity = EXCLUDED.max_capacity,
    updated_at = CURRENT_TIMESTAMP;

-- Remove legacy demo slot codes so the site converges to exactly 48 slots whose
-- slot_code values match the GLB node names. This also removes MOTO-01/MOTO-02
-- from the previous 39-slot seed: motorcycles are capacity-only, never slots.
-- FK order: slot_compatibilities references parking_slots(id) ON DELETE CASCADE,
-- so dependent rows are removed first; structure_slot_audit.slot_id is
-- ON DELETE SET NULL and therefore never blocks the slot delete.
DELETE FROM slot_compatibilities
WHERE site_id = :'site_id'::uuid
  AND slot_id IN (
      SELECT id
      FROM parking_slots
      WHERE site_id = :'site_id'::uuid
        AND slot_code IN ('CAR-01', 'CAR-02', 'CAR-03', 'CAR-04', 'CAR-05', 'CAR-06', 'MOTO-01', 'MOTO-02')
  );

DELETE FROM parking_slots
WHERE site_id = :'site_id'::uuid
  AND slot_code IN ('CAR-01', 'CAR-02', 'CAR-03', 'CAR-04', 'CAR-05', 'CAR-06', 'MOTO-01', 'MOTO-02');

INSERT INTO parking_slots (
    id, tenant_id, site_id, spatial_unit_id, slot_code, slot_type,
    supported_vehicle_type
)
SELECT
    md5('demo-slot-' || v.slot_code)::uuid,
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    :'zone_id'::uuid,
    v.slot_code,
    v.slot_type,
    v.vehicle_type
FROM (VALUES
    ('CarParkingSpace_001', 'STANDARD', 'CAR'),
    ('CarParkingSpace_002', 'STANDARD', 'CAR'),
    ('CarParkingSpace_013', 'STANDARD', 'CAR'),
    ('CarParkingSpace_014', 'STANDARD', 'CAR'),
    ('CarParkingSpace_015', 'STANDARD', 'CAR'),
    ('CarParkingSpace_016', 'STANDARD', 'CAR'),
    ('CarParkingSpace_017', 'STANDARD', 'CAR'),
    ('CarParkingSpace_018', 'STANDARD', 'CAR'),
    ('CarParkingSpace_019', 'STANDARD', 'CAR'),
    ('CarParkingSpace_020', 'STANDARD', 'CAR'),
    ('CarParkingSpace_021', 'STANDARD', 'CAR'),
    ('CarParkingSpace_022', 'STANDARD', 'CAR'),
    ('CarParkingSpace_023', 'STANDARD', 'CAR'),
    ('CarParkingSpace_024', 'STANDARD', 'CAR'),
    ('CarParkingSpace_025', 'STANDARD', 'CAR'),
    ('CarParkingSpace_026', 'STANDARD', 'CAR'),
    ('CarParkingSpace_027', 'STANDARD', 'CAR'),
    ('CarParkingSpace_028', 'STANDARD', 'CAR'),
    ('CarParkingSpace_029', 'STANDARD', 'CAR'),
    ('CarParkingSpace_030', 'STANDARD', 'CAR'),
    ('CarParkingSpace_031', 'STANDARD', 'CAR'),
    ('CarParkingSpace_032', 'STANDARD', 'CAR'),
    ('CarParkingSpace_033', 'STANDARD', 'CAR'),
    ('CarParkingSpace_034', 'STANDARD', 'CAR'),
    ('CarParkingSpace_035', 'STANDARD', 'CAR'),
    ('CarParkingSpace_036', 'STANDARD', 'CAR'),
    ('CarParkingSpace_039', 'STANDARD', 'CAR'),
    ('CarParkingSpace_040', 'STANDARD', 'CAR'),
    ('CarParkingSpace_041', 'STANDARD', 'CAR'),
    ('CarParkingSpace_042', 'STANDARD', 'CAR'),
    ('CarParkingSpace_043', 'STANDARD', 'CAR'),
    ('CarParkingSpace_044', 'STANDARD', 'CAR'),
    ('CarParkingSpace_045', 'STANDARD', 'CAR'),
    ('CarParkingSpace_046', 'STANDARD', 'CAR'),
    ('CarParkingSpace_047', 'STANDARD', 'CAR'),
    ('CarParkingSpace_048', 'STANDARD', 'CAR'),
    ('PARKING_EV_CAR_001', 'EV', 'CAR'),
    ('PARKING_EV_CAR_002', 'EV', 'CAR'),
    ('PARKING_EV_CAR_003', 'EV', 'CAR'),
    ('PARKING_EV_CAR_004', 'EV', 'CAR'),
    ('PARKING_EV_CAR_005', 'EV', 'CAR'),
    ('PARKING_EV_CAR_006', 'EV', 'CAR'),
    ('PARKING_EV_CAR_007', 'EV', 'CAR'),
    ('PARKING_EV_CAR_008', 'EV', 'CAR'),
    ('PARKING_EV_CAR_009', 'EV', 'CAR'),
    ('PARKING_EV_CAR_010', 'EV', 'CAR'),
    ('PARKING_EV_CAR_011', 'EV', 'CAR'),
    ('PARKING_EV_CAR_012', 'EV', 'CAR')
) AS v(slot_code, slot_type, vehicle_type)
ON CONFLICT DO NOTHING;

UPDATE parking_slots s
SET slot_type = v.slot_type,
    supported_vehicle_type = v.vehicle_type,
    updated_at = CURRENT_TIMESTAMP
FROM (VALUES
    ('CarParkingSpace_001', 'STANDARD', 'CAR'),
    ('CarParkingSpace_002', 'STANDARD', 'CAR'),
    ('CarParkingSpace_013', 'STANDARD', 'CAR'),
    ('CarParkingSpace_014', 'STANDARD', 'CAR'),
    ('CarParkingSpace_015', 'STANDARD', 'CAR'),
    ('CarParkingSpace_016', 'STANDARD', 'CAR'),
    ('CarParkingSpace_017', 'STANDARD', 'CAR'),
    ('CarParkingSpace_018', 'STANDARD', 'CAR'),
    ('CarParkingSpace_019', 'STANDARD', 'CAR'),
    ('CarParkingSpace_020', 'STANDARD', 'CAR'),
    ('CarParkingSpace_021', 'STANDARD', 'CAR'),
    ('CarParkingSpace_022', 'STANDARD', 'CAR'),
    ('CarParkingSpace_023', 'STANDARD', 'CAR'),
    ('CarParkingSpace_024', 'STANDARD', 'CAR'),
    ('CarParkingSpace_025', 'STANDARD', 'CAR'),
    ('CarParkingSpace_026', 'STANDARD', 'CAR'),
    ('CarParkingSpace_027', 'STANDARD', 'CAR'),
    ('CarParkingSpace_028', 'STANDARD', 'CAR'),
    ('CarParkingSpace_029', 'STANDARD', 'CAR'),
    ('CarParkingSpace_030', 'STANDARD', 'CAR'),
    ('CarParkingSpace_031', 'STANDARD', 'CAR'),
    ('CarParkingSpace_032', 'STANDARD', 'CAR'),
    ('CarParkingSpace_033', 'STANDARD', 'CAR'),
    ('CarParkingSpace_034', 'STANDARD', 'CAR'),
    ('CarParkingSpace_035', 'STANDARD', 'CAR'),
    ('CarParkingSpace_036', 'STANDARD', 'CAR'),
    ('CarParkingSpace_039', 'STANDARD', 'CAR'),
    ('CarParkingSpace_040', 'STANDARD', 'CAR'),
    ('CarParkingSpace_041', 'STANDARD', 'CAR'),
    ('CarParkingSpace_042', 'STANDARD', 'CAR'),
    ('CarParkingSpace_043', 'STANDARD', 'CAR'),
    ('CarParkingSpace_044', 'STANDARD', 'CAR'),
    ('CarParkingSpace_045', 'STANDARD', 'CAR'),
    ('CarParkingSpace_046', 'STANDARD', 'CAR'),
    ('CarParkingSpace_047', 'STANDARD', 'CAR'),
    ('CarParkingSpace_048', 'STANDARD', 'CAR'),
    ('PARKING_EV_CAR_001', 'EV', 'CAR'),
    ('PARKING_EV_CAR_002', 'EV', 'CAR'),
    ('PARKING_EV_CAR_003', 'EV', 'CAR'),
    ('PARKING_EV_CAR_004', 'EV', 'CAR'),
    ('PARKING_EV_CAR_005', 'EV', 'CAR'),
    ('PARKING_EV_CAR_006', 'EV', 'CAR'),
    ('PARKING_EV_CAR_007', 'EV', 'CAR'),
    ('PARKING_EV_CAR_008', 'EV', 'CAR'),
    ('PARKING_EV_CAR_009', 'EV', 'CAR'),
    ('PARKING_EV_CAR_010', 'EV', 'CAR'),
    ('PARKING_EV_CAR_011', 'EV', 'CAR'),
    ('PARKING_EV_CAR_012', 'EV', 'CAR')
) AS v(slot_code, slot_type, vehicle_type)
WHERE s.site_id = :'site_id'::uuid
  AND s.deleted_at IS NULL
  AND s.slot_code = v.slot_code;

INSERT INTO slot_compatibilities (tenant_id, site_id, slot_id, vehicle_type)
SELECT
    :'tenant_id'::uuid,
    :'site_id'::uuid,
    s.id,
    s.supported_vehicle_type
FROM parking_slots s
WHERE s.site_id = :'site_id'::uuid
  AND s.deleted_at IS NULL
ON CONFLICT (slot_id, vehicle_type) DO NOTHING;

COMMIT;
