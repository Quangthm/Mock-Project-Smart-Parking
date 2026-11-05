-- PARKING DB
BEGIN;
CREATE TABLE IF NOT EXISTS structure_synchronizations (
    token UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id),
    operation_id UUID REFERENCES structure_operations(id),
    state TEXT NOT NULL CHECK (state IN ('preparing','aborting','ready','completed')),
    outcome JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (state <> 'ready' OR outcome IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS ix_structure_synchronizations_pending
    ON structure_synchronizations(created_at) WHERE state <> 'completed';
COMMIT;
