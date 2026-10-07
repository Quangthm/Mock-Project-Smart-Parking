-- ParkingService only; apply after the Parking alignment overlay.
BEGIN;
CREATE TABLE IF NOT EXISTS structure_backup_policies (
 id UUID PRIMARY KEY, site_id UUID NOT NULL REFERENCES parking_sites(id), unit_id UUID REFERENCES spatial_units(id),
 vehicle_type TEXT NOT NULL CHECK(vehicle_type IN ('CAR','MOTORCYCLE','OVERSIZED')), count INT NOT NULL CHECK(count>=0));
CREATE UNIQUE INDEX IF NOT EXISTS uq_backup_site ON structure_backup_policies(site_id,vehicle_type) WHERE unit_id IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_backup_unit ON structure_backup_policies(site_id,unit_id,vehicle_type) WHERE unit_id IS NOT NULL;
CREATE TABLE IF NOT EXISTS structure_operations (
 id UUID PRIMARY KEY, site_id UUID NOT NULL REFERENCES parking_sites(id), tenant_id UUID NOT NULL,
 actor_id UUID NOT NULL, idempotency_key UUID NOT NULL, request JSONB NOT NULL, status TEXT NOT NULL,
 impact JSONB, reason TEXT, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 UNIQUE(site_id,idempotency_key),CHECK(status IN ('pending','failed','completed')));
ALTER TABLE structure_operations ADD COLUMN IF NOT EXISTS physical_committed BOOLEAN NOT NULL DEFAULT FALSE;
CREATE TABLE IF NOT EXISTS structure_slot_audit (
 id UUID PRIMARY KEY, site_id UUID NOT NULL REFERENCES parking_sites(id),slot_id UUID NOT NULL REFERENCES parking_slots(id),
 actor_id UUID NOT NULL,action TEXT NOT NULL,reason TEXT NOT NULL,created_at TIMESTAMPTZ NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS structure_change_audit (
 id UUID PRIMARY KEY,site_id UUID NOT NULL REFERENCES parking_sites(id),actor_id UUID NOT NULL,
 operation_id UUID REFERENCES structure_operations(id),before_state JSONB,after_state JSONB NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS ix_structure_change_history ON structure_change_audit(site_id,created_at);
COMMIT;
