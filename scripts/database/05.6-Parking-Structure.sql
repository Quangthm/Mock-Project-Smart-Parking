-- SPARK-188. Apply after 05.1. Re-runnable; no seed/demo tenant is created.
BEGIN;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_site_code_ci ON parking_sites(tenant_id, upper(site_code)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_slot_code_ci ON parking_slots(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_unit_name_ci ON spatial_units(site_id, parent_id, lower(name)) NULLS NOT DISTINCT WHERE deleted_at IS NULL;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_deleted_slot_not_occupied' AND conrelid='parking_slots'::regclass) THEN
        ALTER TABLE parking_slots ADD CONSTRAINT ck_deleted_slot_not_occupied
            CHECK (deleted_at IS NULL OR (NOT is_physically_occupied AND operational_status <> 'UNKNOWN')) NOT VALID;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS parking_access_paths (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    path_code VARCHAR(50) NOT NULL,
    from_unit_id UUID REFERENCES spatial_units(id) ON DELETE RESTRICT,
    to_unit_id UUID REFERENCES spatial_units(id) ON DELETE RESTRICT,
    map_data JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ,
    CHECK (from_unit_id IS NULL OR to_unit_id IS NULL OR from_unit_id <> to_unit_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_access_path_code ON parking_access_paths(site_id, upper(path_code)) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_access_paths_site ON parking_access_paths(site_id);

-- Serialize new operational commitments with layout edits, including writes from other services.
-- Existing released/historical references remain valid after soft deletion.
CREATE OR REPLACE FUNCTION guard_parking_structure_commitment() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE target_slot UUID; target_unit UUID; live BOOLEAN;
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.site_id <> OLD.site_id THEN
        RAISE EXCEPTION 'A commitment cannot be transferred to another site' USING ERRCODE = '23514';
    END IF;
    PERFORM 1 FROM parking_sites WHERE id = NEW.site_id FOR UPDATE;
    IF TG_TABLE_NAME = 'reservations' THEN
        live := NEW.status IN ('PENDING_PAYMENT', 'CONFIRMED', 'CHECKED_IN');
        target_slot := NEW.target_slot_id; target_unit := NEW.target_spatial_unit_id;
    ELSIF TG_TABLE_NAME = 'slot_allocations' THEN
        live := NEW.allocation_status IN ('RESERVED', 'OCCUPIED'); target_slot := NEW.slot_id;
    ELSIF TG_TABLE_NAME = 'parking_sessions' THEN
        live := NEW.exit_time IS NULL; target_slot := NEW.current_slot_id;
    ELSE
        live := NEW.total_capacity > 0 OR NEW.current_reserved_count > 0 OR NEW.emergency_backup_quota > 0;
        target_unit := NEW.spatial_unit_id;
    END IF;
    IF live THEN
        IF NOT EXISTS (SELECT 1 FROM parking_sites WHERE id=NEW.site_id AND is_active AND deleted_at IS NULL) THEN
            RAISE EXCEPTION 'Parking site is inactive' USING ERRCODE = '23514';
        END IF;
        IF target_slot IS NOT NULL AND NOT EXISTS (SELECT 1 FROM parking_slots WHERE id=target_slot AND site_id=NEW.site_id AND deleted_at IS NULL) THEN
            RAISE EXCEPTION 'Parking slot is outside the active site structure' USING ERRCODE = '23514';
        END IF;
        IF target_unit IS NOT NULL AND NOT EXISTS (SELECT 1 FROM spatial_units WHERE id=target_unit AND site_id=NEW.site_id AND deleted_at IS NULL) THEN
            RAISE EXCEPTION 'Spatial unit is outside the active site structure' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS guard_structure_reservation ON reservations;
CREATE TRIGGER guard_structure_reservation BEFORE INSERT OR UPDATE ON reservations FOR EACH ROW EXECUTE FUNCTION guard_parking_structure_commitment();
DROP TRIGGER IF EXISTS guard_structure_allocation ON slot_allocations;
CREATE TRIGGER guard_structure_allocation BEFORE INSERT OR UPDATE ON slot_allocations FOR EACH ROW EXECUTE FUNCTION guard_parking_structure_commitment();
DROP TRIGGER IF EXISTS guard_structure_session ON parking_sessions;
CREATE TRIGGER guard_structure_session BEFORE INSERT OR UPDATE ON parking_sessions FOR EACH ROW EXECUTE FUNCTION guard_parking_structure_commitment();
DROP TRIGGER IF EXISTS guard_structure_capacity ON site_capacity_pools;
CREATE TRIGGER guard_structure_capacity BEFORE INSERT OR UPDATE ON site_capacity_pools FOR EACH ROW EXECUTE FUNCTION guard_parking_structure_commitment();
COMMIT;
