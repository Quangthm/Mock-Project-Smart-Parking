-- Demo facility seed for ParkingDb.
-- Creates one tenant copy, one site, a floor + zone, eight slots and static capacity.
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
    8,
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
    8
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
    8
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
    (:'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR', 6),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'MOTORCYCLE', 2)
ON CONFLICT (spatial_unit_id, vehicle_type) DO UPDATE
SET max_capacity = EXCLUDED.max_capacity,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO parking_slots (
    id, tenant_id, site_id, spatial_unit_id, slot_code, slot_type,
    supported_vehicle_type
)
VALUES
    (:'slot1'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-01', 'STANDARD', 'CAR'),
    (:'slot2'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-02', 'STANDARD', 'CAR'),
    (:'slot3'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-03', 'STANDARD', 'CAR'),
    (:'slot4'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-04', 'STANDARD', 'CAR'),
    (:'slot5'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-05', 'STANDARD', 'CAR'),
    (:'slot6'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'CAR-06', 'STANDARD', 'CAR'),
    (:'slot7'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'MOTO-01', 'STANDARD', 'MOTORCYCLE'),
    (:'slot8'::uuid, :'tenant_id'::uuid, :'site_id'::uuid, :'zone_id'::uuid, 'MOTO-02', 'STANDARD', 'MOTORCYCLE')
ON CONFLICT (id) DO UPDATE
SET tenant_id = EXCLUDED.tenant_id,
    site_id = EXCLUDED.site_id,
    spatial_unit_id = EXCLUDED.spatial_unit_id,
    slot_code = EXCLUDED.slot_code,
    slot_type = EXCLUDED.slot_type,
    supported_vehicle_type = EXCLUDED.supported_vehicle_type,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO slot_compatibilities (tenant_id, site_id, slot_id, vehicle_type)
VALUES
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot1'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot2'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot3'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot4'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot5'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot6'::uuid, 'CAR'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot7'::uuid, 'MOTORCYCLE'),
    (:'tenant_id'::uuid, :'site_id'::uuid, :'slot8'::uuid, 'MOTORCYCLE')
ON CONFLICT (slot_id, vehicle_type) DO NOTHING;

COMMIT;
