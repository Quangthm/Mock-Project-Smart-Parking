-- Parking DB only. Optional map coordinates required by FR-LOT-03; original schema stays intact.
BEGIN;
ALTER TABLE parking_slots ADD COLUMN IF NOT EXISTS coordinates_3d JSONB;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_deleted_slot_safe' AND conrelid='parking_slots'::regclass) THEN
        ALTER TABLE parking_slots ADD CONSTRAINT ck_deleted_slot_safe
            CHECK (deleted_at IS NULL OR (physical_state NOT IN ('OCCUPIED','UNKNOWN') AND reservation_state IS NULL)) NOT VALID;
    END IF;
END $$;
-- Keep identifier history reserved, consistently with the base schema's non-partial UNIQUE constraints.
CREATE UNIQUE INDEX IF NOT EXISTS uq_site_slot_code_ci ON parking_slots(tenant_id, site_id, upper(slot_code));
COMMIT;
