-- SPARK-238 Â· facility structure maintenance windows (Q3, A4, A10, A11; review F03, F04)
-- Approved by PM 2026-10-29. Owned by Facility.
--
-- Deliberately NOT added to the hard-coded overlay list in
-- scripts/apply-workflow-migrations.ps1 yet: that file is shared with the SPARK-228
-- lane, so the ordering entry is added at merge time to avoid a conflict.
--
-- Column naming: the wire field is `window`, but the column is `service_window`
-- because WINDOW is a reserved keyword in PostgreSQL and cannot be used unquoted
-- as a column name (`CREATE TABLE t (window tstzrange)` is a syntax error).
--
-- Idempotent: safe to re-run against an existing database.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
-- Required by the EXCLUDE constraint below (equality operator on UUID).
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE IF NOT EXISTS structure_maintenance_windows (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    -- Scope is either the whole site or a spatial unit; a slot-level window
    -- additionally narrows a unit-level one (ck_window_scope).
    unit_id UUID NULL REFERENCES spatial_units(id) ON DELETE CASCADE,
    slot_id UUID NULL REFERENCES parking_slots(id) ON DELETE CASCADE,
    service_window TSTZRANGE NOT NULL,
    reason TEXT NOT NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    cancelled_at TIMESTAMPTZ NULL,
    -- F04: a window is not "in force" until admission enforcement acknowledges it.
    -- 'pending' is never reported as a closed lot; reads surface MAINTENANCE_UNCERTAIN.
    enforcement_state TEXT NOT NULL DEFAULT 'pending'
        CHECK (enforcement_state IN ('pending', 'acknowledged', 'failed')),
    enforcement_ref UUID NULL,
    acknowledged_at TIMESTAMPTZ NULL,
    CONSTRAINT ck_window_nonempty CHECK (NOT isempty(service_window)),
    CONSTRAINT ck_window_scope CHECK (slot_id IS NULL OR unit_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_maintenance_windows_site_time
    ON structure_maintenance_windows USING gist (site_id, service_window);

-- Same-database concurrency guard: two live site-wide windows can never overlap,
-- whatever the application read just before inserting. This protects Facility
-- against itself only; it cannot serialize a booking accepted in Reservation
-- (see the enforcement protocol in the API design).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ex_maintenance_windows_site_overlap'
          AND conrelid = 'structure_maintenance_windows'::regclass
    ) THEN
        ALTER TABLE structure_maintenance_windows
            ADD CONSTRAINT ex_maintenance_windows_site_overlap
            EXCLUDE USING gist (site_id WITH =, service_window WITH &&)
            WHERE (cancelled_at IS NULL AND unit_id IS NULL AND slot_id IS NULL);
    END IF;
END
$$;
