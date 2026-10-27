-- Overlay: parking slot constraints from integration/02
-- Contains ONLY: ck_deleted_slot_safe + uq_site_slot_code_ci (case-insensitive site+slot unique code index)
-- Integration/02 partial overlay (T3 §3.3)
BEGIN;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_deleted_slot_safe' AND conrelid='parking_slots'::regclass) THEN
        ALTER TABLE parking_slots ADD CONSTRAINT ck_deleted_slot_safe
            CHECK (deleted_at IS NULL OR (physical_state NOT IN ('OCCUPIED','UNKNOWN') AND reservation_state IS NULL)) NOT VALID;
    END IF;
END $$;
-- Keep identifier history reserved, consistently with the base schema's non-partial UNIQUE constraints.
CREATE UNIQUE INDEX IF NOT EXISTS uq_site_slot_code_ci ON parking_slots(tenant_id, site_id, upper(slot_code));
COMMIT;
