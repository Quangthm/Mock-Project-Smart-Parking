-- ReservationService only. Historical confirmation time is deliberately unknown.
BEGIN;
ALTER TABLE reservations ADD COLUMN IF NOT EXISTS confirmed_at TIMESTAMPTZ;
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
