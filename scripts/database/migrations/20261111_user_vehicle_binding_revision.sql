-- Manual upgrade for an existing UserService database whose vehicles table
-- predates the SPARK-243 binding revision. Back up the target database first.
-- Revision 1 matches the canonical CREATE TABLE baseline for existing bindings.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public.vehicles
    ADD COLUMN IF NOT EXISTS binding_revision BIGINT NOT NULL DEFAULT 1;

DO $check$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_attribute
        WHERE attrelid = 'public.vehicles'::regclass
          AND attname = 'binding_revision' AND NOT attisdropped
          AND atttypid = 'bigint'::regtype AND attnotnull
    ) THEN
        RAISE EXCEPTION 'Unexpected vehicles.binding_revision schema; manual review required';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'public.vehicles'::regclass
          AND conname = 'vehicles_binding_revision_check'
    ) THEN
        ALTER TABLE public.vehicles
            ADD CONSTRAINT vehicles_binding_revision_check CHECK (binding_revision > 0);
    END IF;
END $check$;
COMMIT;

-- Keep this additive column when rolling back application binaries. Removing
-- it would discard binding versions used by current UserService callers.
