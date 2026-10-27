-- ======================================================================================
-- OVERLAY 4/4 — Reservation confirmation-time trigger (smartpark_reservation)
-- Purpose : record_reservation_confirmation() + reservation_confirmation_time trigger on
--           reservations — stamps confirmed_at when a reservation first reaches
--           CONFIRMED/ALLOCATED/PARKING. Missing from the post-split base schema
--           (plan gap R4); nothing in the BE sets confirmed_at, only reads it.
-- Target  : smartpark_reservation
-- Order   : APPLY FOURTH (4th) — after reservation-site-capacity-pools.sql and
--           reservation-structure-safety.sql (both install BEFORE INSERT OR UPDATE
--           triggers on reservations; apply this last so guard_structure_edit runs first).
-- Plan ref: T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md §2.2 R4, §3.3.
-- Evidence: StructureSafetyStore.cs:19, 23 (SELECT/ORDER BY confirmed_at; no BE writer).
-- ======================================================================================
-- Reproduced from scripts/database/05.10-Reservation-Confirmation-Time.sql, except:
-- the ALTER TABLE ... ADD COLUMN confirmed_at is intentionally OMITTED — the column
-- already exists on reservations in the new base file
-- 03b-reservation-operations-service-db.sql, so no ALTER is needed (R4).
-- ======================================================================================
BEGIN;
CREATE OR REPLACE FUNCTION record_reservation_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.status IN ('CONFIRMED','ALLOCATED','PARKING') AND NEW.confirmed_at IS NULL THEN
  IF TG_OP='INSERT' THEN NEW.confirmed_at=clock_timestamp();
  ELSIF OLD.status NOT IN ('CONFIRMED','ALLOCATED','PARKING') THEN NEW.confirmed_at=clock_timestamp();
  END IF;
 END IF;
 IF TG_OP='UPDATE' AND OLD.confirmed_at IS NOT NULL THEN NEW.confirmed_at=OLD.confirmed_at; END IF;
 RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS reservation_confirmation_time ON reservations;
CREATE TRIGGER reservation_confirmation_time BEFORE INSERT OR UPDATE ON reservations
 FOR EACH ROW EXECUTE FUNCTION record_reservation_confirmation();
COMMIT;
