-- ReservationService only; apply after the structure safety overlay.

BEGIN;
CREATE TABLE IF NOT EXISTS structure_release_receipts (
    token UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    outcome JSONB NOT NULL,
    completed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
COMMIT;
