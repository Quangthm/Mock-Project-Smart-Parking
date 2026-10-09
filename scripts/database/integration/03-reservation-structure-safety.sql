-- Reservation DB only. No triggers, joins or physical FKs to Parking/User databases.
BEGIN;
CREATE TABLE IF NOT EXISTS structure_edit_holds (
    site_id UUID PRIMARY KEY, tenant_id UUID NOT NULL, token UUID NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS structure_site_state (site_id UUID PRIMARY KEY, tenant_id UUID NOT NULL, active BOOLEAN NOT NULL);
CREATE TABLE IF NOT EXISTS structure_removed_resources (id UUID PRIMARY KEY, site_id UUID NOT NULL, kind TEXT NOT NULL CHECK(kind IN ('slot','unit')));
CREATE OR REPLACE FUNCTION guard_structure_edit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE slot UUID; unit UUID; live BOOLEAN;
BEGIN
    IF TG_OP='UPDATE' AND (NEW.site_id<>OLD.site_id OR NEW.tenant_id<>OLD.tenant_id) THEN
        RAISE EXCEPTION 'Commitment scope cannot change' USING ERRCODE='23514';
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.site_id::text, 0));
    IF EXISTS (SELECT 1 FROM structure_edit_holds WHERE site_id=NEW.site_id) THEN
        RAISE EXCEPTION 'Structure edit is in progress; retry later' USING ERRCODE='55P03';
    END IF;
    IF TG_TABLE_NAME='reservations' THEN
        live := NEW.status IN ('PENDING_PAYMENT','CONFIRMED','ALLOCATED','PARKING');
        slot := NEW.target_slot_id; unit := NEW.target_spatial_unit_id;
    ELSIF TG_TABLE_NAME='parking_sessions' THEN
        live := NEW.exit_time IS NULL; slot := NEW.current_slot_id;
    ELSIF TG_TABLE_NAME='slot_allocations' THEN
        live := NEW.allocation_status IN ('RESERVED','OCCUPIED'); slot := NEW.slot_id;
    ELSE
        live := NEW.current_reserved_count>0 OR NEW.pending_payment_count>0
            OR NEW.occupied_count>0 OR NEW.protected_count>0 OR NEW.backup_count>0;
        unit := NEW.spatial_unit_id;
        IF NEW.total_capacity>0 AND EXISTS (SELECT 1 FROM structure_removed_resources WHERE id=unit) THEN
            RAISE EXCEPTION 'Capacity unit is removed' USING ERRCODE='23514';
        END IF;
    END IF;
    IF live AND (
        EXISTS (SELECT 1 FROM structure_site_state WHERE site_id=NEW.site_id AND (NOT active OR tenant_id<>NEW.tenant_id))
        OR EXISTS (SELECT 1 FROM structure_removed_resources WHERE (id=slot OR id=unit))
    ) THEN RAISE EXCEPTION 'Resource is removed or inactive' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS guard_structure_edit ON reservations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON reservations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON parking_sessions;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON parking_sessions FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON slot_allocations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON slot_allocations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON site_capacity_pools;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON site_capacity_pools FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
COMMIT;
