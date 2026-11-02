-- Reservation DB only. No triggers, joins or physical FKs to Parking/User databases.
-- Reference mirror of overlay/reservation-structure-safety.sql (the applied copy).
-- structure_removed_resources is dropped: it was only written by the release path and
-- could never hold a resource referenced by a live commitment. DROP runs after
-- CREATE OR REPLACE FUNCTION because plpgsql resolves table names at runtime.
BEGIN;
CREATE TABLE IF NOT EXISTS structure_edit_holds (
    site_id UUID PRIMARY KEY, tenant_id UUID NOT NULL, token UUID NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS structure_site_state (site_id UUID PRIMARY KEY, tenant_id UUID NOT NULL, active BOOLEAN NOT NULL);
CREATE OR REPLACE FUNCTION guard_structure_edit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE live BOOLEAN;
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
    ELSIF TG_TABLE_NAME='parking_sessions' THEN
        live := NEW.exit_time IS NULL;
    ELSIF TG_TABLE_NAME='slot_allocations' THEN
        live := NEW.allocation_status IN ('RESERVED','OCCUPIED');
    ELSE
        live := NEW.current_reserved_count>0 OR NEW.pending_payment_count>0
            OR NEW.occupied_count>0 OR NEW.protected_count>0 OR NEW.backup_count>0;
    END IF;
    IF live AND EXISTS (
        SELECT 1 FROM structure_site_state WHERE site_id=NEW.site_id AND (NOT active OR tenant_id<>NEW.tenant_id)
    ) THEN RAISE EXCEPTION 'Resource is inactive' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END $$;
DROP TABLE IF EXISTS structure_removed_resources;
DROP TRIGGER IF EXISTS guard_structure_edit ON reservations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON reservations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON parking_sessions;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON parking_sessions FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON slot_allocations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON slot_allocations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON site_capacity_pools;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON site_capacity_pools FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
COMMIT;
