-- ======================================================================================
-- OVERLAY 2/4 — Reservation structure-safety gates (smartpark_reservation)
-- Purpose : Structure-edit hold/state tables + the guard_structure_edit() trigger
--           function and its 4 triggers (reservations, parking_sessions,
--           slot_allocations, site_capacity_pools). Missing from the post-split base
--           schema (plan gap R1).
-- Target  : smartpark_reservation
-- Order   : APPLY SECOND (2nd) — MUST run AFTER reservation-site-capacity-pools.sql
--           because the 4th trigger references site_capacity_pools. Before
--           reservation-structure-receipts.sql and reservation-confirmation-time.sql.
-- Plan ref: T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md §2.2 R1, §3.3.
-- Evidence: StructureSafetyStore.cs:81 (lock-key contract hashtextextended(site_id,0)),
--           :92-96 (holds/token flow), :127 (inactive-site check).
-- Change  : structure_removed_resources was dropped. It was written only by the release
--           path, and removal of a slot/unit requires zero live commitments, so the
--           table could never hold a resource referenced by a live commitment. The
--           guard therefore enforces only the inactive-site and in-progress-edit rules.
--           ParkingService re-registers a removed slot code by resurrecting the
--           soft-deleted parking_slots row (deleted_at -> NULL), not by re-inserting.
--           DROP runs after CREATE OR REPLACE FUNCTION on purpose: plpgsql resolves
--           table names at runtime, so dropping first would raise 42P01 for writers.
-- ======================================================================================
-- Reproduced from scripts/database/integration/03-reservation-structure-safety.sql.
-- Idempotency: IF NOT EXISTS on tables; CREATE OR REPLACE for the function; triggers
-- use DROP TRIGGER IF EXISTS + CREATE TRIGGER (same pattern as integration/03 —
-- CREATE TRIGGER has no IF NOT EXISTS in older PostgreSQL).
-- Reservation DB only. No triggers, joins or physical FKs to Parking/User databases.
-- ======================================================================================
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
