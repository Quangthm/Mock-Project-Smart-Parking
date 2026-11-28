-- Checkpoint A synthetic publication exception applies ONLY to these deterministic sites.
-- Additional-site grants have no Owner application or onboarding receipt.
SELECT pg_temp.require(current_database()='smartpark_facility' AND current_user='smartpark_facility','Unexpected FacilityDb identity');
LOCK TABLE public.tenants,parking_sites,owner_onboarding_provisions,spatial_units,spatial_unit_capacities,
    parking_slots,slot_compatibilities,structure_backup_policies,facility_discovery_publications IN SHARE ROW EXCLUSIVE MODE;
SELECT pg_temp.require((SELECT count(*)=3 FROM pg_trigger WHERE
    (tgrelid,tgname) IN (('parking_sites'::regclass,'discovery_public_site_edited'),
    ('tenants'::regclass,'discovery_tenant_changed'),('facility_discovery_publications'::regclass,'discovery_membership_changed'))
    AND tgenabled IN ('O','A')) AND (SELECT count(*)=1 FROM facility_discovery_version WHERE singleton AND revision IS NOT NULL),
    'Discovery publication migration/triggers are missing or disabled');
SELECT pg_temp.require((SELECT count(*)=3 FROM pg_index WHERE indisvalid AND indisunique AND indexrelid IN
    (to_regclass('uq_site_slot_code_ci'),to_regclass('uq_backup_site_policy'),to_regclass('uq_active_site_code_ci'))),
    'Facility identity/backup indexes are missing');
UPDATE run_context SET established=EXISTS(SELECT FROM public.tenants WHERE id=(SELECT id FROM site_input WHERE n='01'));
CREATE TEMP TABLE e_tenants AS SELECT id,tenant_code AS code,business AS name,email AS contact_email,phone AS contact_phone,
    'ACTIVE'::text AS status,NULL::timestamptz AS deleted_at,t-interval '120 days' AS created_at FROM owner_input,run_context;
SELECT pg_temp.sync('tenants','id',ARRAY['created_at']);

CREATE TEMP TABLE e_parking_sites AS SELECT id,tenant AS tenant_id,code AS site_code,name,address,lat AS latitude,lon AS longitude,
    capacity AS total_physical_capacity,'ACTIVE'::text AS status,NULL::timestamptz AS deleted_at,t-interval '120 days' AS created_at
    FROM site_input,run_context;
SELECT pg_temp.sync('parking_sites','id',ARRAY['name','address','latitude','longitude','created_at']);
CREATE TEMP TABLE e_owner_onboarding_provisions AS SELECT id AS application_id,id AS site_id,
    jsonb_build_object('TenantId',id,'Name',business,'Email',email,'Phone',phone,'Lot',lot) AS payload FROM owner_input;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM owner_onboarding_provisions p JOIN site_input s ON s.id=p.site_id
    WHERE NOT s.initial), 'Unexpected additional-site onboarding evidence');
SELECT pg_temp.sync('owner_onboarding_provisions','application_id');

CREATE TEMP VIEW floor_input AS SELECT s.id AS site_id,s.tenant AS tenant_id,f.* FROM site_input s,
    jsonb_to_recordset(s.floors) f(id uuid,name text,zone uuid,slots jsonb,"floorCapacity" uuid,"zoneCapacity" uuid);
CREATE TEMP TABLE e_spatial_units AS
SELECT id,tenant_id,site_id,NULL::uuid AS parent_id,format('/%s/%s/',site_id,id) AS path,
    'FLOOR'::text AS unit_type,name,jsonb_array_length(slots) AS max_capacity,NULL::timestamptz AS deleted_at FROM floor_input
UNION ALL
SELECT zone,tenant_id,site_id,id,format('/%s/%s/%s/',site_id,id,zone),'ZONE','Zone '||name,jsonb_array_length(slots),NULL FROM floor_input;
SELECT pg_temp.sync('spatial_units','id');
CREATE TEMP TABLE e_spatial_unit_capacities AS
SELECT "floorCapacity" AS id,tenant_id,site_id,id AS spatial_unit_id,'CAR'::text AS vehicle_type,jsonb_array_length(slots) AS max_capacity FROM floor_input
UNION ALL SELECT "zoneCapacity",tenant_id,site_id,zone,'CAR',jsonb_array_length(slots) FROM floor_input;
SELECT pg_temp.sync('spatial_unit_capacities','id');

CREATE TEMP VIEW bay_input AS SELECT f.tenant_id,f.site_id,f.zone,b.* FROM floor_input f,
    jsonb_to_recordset(f.slots) b(id uuid,code text,kind text,backup boolean,compatibility uuid);
CREATE TEMP TABLE e_parking_slots AS SELECT id,tenant_id,site_id,zone AS spatial_unit_id,code AS slot_code,kind AS slot_type,
    'CAR'::text AS supported_vehicle_type,'AVAILABLE'::text AS physical_state,
    CASE WHEN backup THEN 'BACKUP'::text END AS reservation_state,NULL::timestamptz AS deleted_at FROM bay_input;
-- Match an already established C phase during the foundation check. The
-- separate C transaction verifies the timestamps and owns every physical write.
UPDATE e_parking_slots e SET physical_state='OCCUPIED'
FROM parking_slots p JOIN lifecycle_occupancy o USING(id)
WHERE e.id=o.id AND p.site_id=o.site_id AND p.tenant_id=o.tenant_id AND p.physical_state='OCCUPIED';
SELECT pg_temp.require(NOT EXISTS(SELECT FROM parking_slots p JOIN e_parking_slots e USING(id)
    WHERE p.reservation_state IN ('RESERVED','PROTECTED')), 'Live slot protection requires reconciliation');
SELECT pg_temp.sync('parking_slots','id',ARRAY['reservation_state']);
CREATE TEMP TABLE e_slot_compatibilities AS SELECT compatibility AS id,tenant_id,site_id,id AS slot_id,'CAR'::text AS vehicle_type FROM bay_input;
SELECT pg_temp.sync('slot_compatibilities','id');
CREATE TEMP TABLE e_structure_backup_policies AS SELECT policy AS id,tenant AS tenant_id,id AS site_id,NULL::uuid AS unit_id,
    'CAR'::text AS vehicle_type,backup AS count FROM site_input;
SELECT pg_temp.sync('structure_backup_policies','id',ARRAY['count']);

-- Public fields are finalized above: a profile edit can remove its grant inside this transaction.
-- The whole Facility phase commits together. A missing grant on an established site means
-- withdrawal/invalidation or tampering; ordinary replays stop. Only explicit reset can restore it.
CREATE TEMP TABLE e_facility_discovery_publications AS SELECT id AS site_id,tenant AS tenant_id,publication AS configuration_version,
    replace(type,'-','_') AS lot_type,ARRAY['CAR'] AS categories,NULL::timestamptz AS window_start,
    NULL::timestamptz AS window_end,NULL::text AS window_state FROM site_input;
UPDATE e_facility_discovery_publications e SET configuration_version =
    CASE WHEN (p.lot_type,p.categories,p.window_start,p.window_end,p.window_state)
              IS DISTINCT FROM (e.lot_type,e.categories,e.window_start,e.window_end,e.window_state)
         THEN gen_random_uuid() ELSE p.configuration_version END
    FROM facility_discovery_publications p WHERE p.site_id=e.site_id;
UPDATE e_facility_discovery_publications e SET configuration_version=gen_random_uuid()
    FROM run_context c WHERE c.established AND c.reset AND NOT EXISTS
    (SELECT FROM facility_discovery_publications p WHERE p.site_id=e.site_id);
SELECT pg_temp.sync('facility_discovery_publications','site_id',ARRAY['configuration_version','lot_type','categories','window_start','window_end','window_state'],
    ARRAY[]::text[],true);

SELECT pg_temp.require(NOT EXISTS(SELECT FROM parking_slots p JOIN site_input s ON p.site_id=s.id
    WHERE NOT EXISTS(SELECT FROM e_parking_slots e WHERE e.id=p.id))
    AND NOT EXISTS(SELECT FROM spatial_units p JOIN site_input s ON p.site_id=s.id
    WHERE NOT EXISTS(SELECT FROM e_spatial_units e WHERE e.id=p.id))
    AND NOT EXISTS(SELECT FROM structure_backup_policies p JOIN site_input s ON p.site_id=s.id
    WHERE NOT EXISTS(SELECT FROM e_structure_backup_policies e WHERE e.id=p.id))
    AND NOT EXISTS(SELECT FROM slot_compatibilities p JOIN e_parking_slots s ON p.slot_id=s.id
    WHERE NOT EXISTS(SELECT FROM e_slot_compatibilities e WHERE e.id=p.id)),
    'Additional structure or compatibility data found; preserve and reconcile before replay');

SELECT format('Facility persisted: %s sites, %s grants (%s initial), %s floors, %s zones, %s CAR bays, %s backups',
    (SELECT count(*) FROM parking_sites JOIN site_input USING(id)),
    (SELECT count(*) FROM facility_discovery_publications p JOIN site_input s ON p.site_id=s.id),
    (SELECT count(*) FROM owner_onboarding_provisions p JOIN owner_input s ON p.site_id=s.id),
    (SELECT count(*) FROM spatial_units p JOIN e_spatial_units e USING(id) WHERE p.unit_type='FLOOR'),
    (SELECT count(*) FROM spatial_units p JOIN e_spatial_units e USING(id) WHERE p.unit_type='ZONE'),
    (SELECT count(*) FROM parking_slots JOIN e_parking_slots USING(id)),
    (SELECT count(*) FROM parking_slots p JOIN e_parking_slots e USING(id) WHERE p.reservation_state='BACKUP'))
FROM run_context WHERE mode='verify';

-- CHECKPOINT C
SELECT pg_temp.require(current_database()='smartpark_facility' AND current_user='smartpark_facility','Unexpected FacilityDb identity');
LOCK TABLE parking_slots,structure_maintenance_windows IN SHARE ROW EXCLUSIVE MODE;
SELECT pg_temp.require((SELECT count(*)=12 FROM lifecycle_occupancy),'Expected twelve located C sessions');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM structure_maintenance_windows w JOIN site_input s ON s.id=w.site_id
    WHERE w.cancelled_at IS NULL AND upper(w.service_window) > (SELECT (j->>'begins')::timestamptz FROM lifecycle_manifest)),
    'Fixture lifecycle intersects a maintenance window or requires availability reconciliation');
SELECT pg_temp.require(
    CASE WHEN (SELECT mode='verify' FROM run_context) THEN
        (SELECT count(*)=12 FROM lifecycle_occupancy o JOIN parking_slots p USING(id)
            WHERE p.site_id=o.site_id AND p.tenant_id=o.tenant_id AND p.deleted_at IS NULL
            AND p.physical_state='OCCUPIED' AND p.reservation_state IS NULL AND p.updated_at=o.updated_at)
    WHEN current_setting('mvp.operations_established')::bool THEN
        (SELECT count(*)=12 FROM lifecycle_occupancy o JOIN prior_occupancy prior USING(id) JOIN parking_slots p USING(id)
            WHERE p.site_id=o.site_id AND p.tenant_id=o.tenant_id AND p.deleted_at IS NULL
            AND p.physical_state='OCCUPIED' AND p.reservation_state IS NULL AND p.updated_at=prior.updated_at)
    WHEN (SELECT mode='check' FROM run_context) AND NOT EXISTS(SELECT FROM parking_sites p JOIN site_input s USING(id)) THEN true
    ELSE
        (SELECT count(*)=12 FROM lifecycle_occupancy o JOIN parking_slots p USING(id)
            WHERE p.site_id=o.site_id AND p.tenant_id=o.tenant_id AND p.deleted_at IS NULL
            AND p.physical_state='AVAILABLE' AND p.reservation_state IS NULL)
    END,
    'C occupancy is missing, ambiguous or progressed. Inspect the exact Operations scans and Facility state before recovery.');
UPDATE parking_slots p SET physical_state='OCCUPIED',updated_at=o.updated_at
FROM lifecycle_occupancy o,run_context c
WHERE p.id=o.id AND c.mode='apply' AND (p.physical_state,p.updated_at) IS DISTINCT FROM ('OCCUPIED',o.updated_at);

