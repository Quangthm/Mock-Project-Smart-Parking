BEGIN;

ALTER TABLE parking_sessions
    ADD COLUMN IF NOT EXISTS location_scanned_at TIMESTAMPTZ;

COMMIT;
