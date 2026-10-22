-- FacilityService database; apply before deploying the new binaries. Re-runnable.
BEGIN;
CREATE TABLE IF NOT EXISTS owner_onboarding_provisions (
    application_id uuid PRIMARY KEY,
    site_id uuid NOT NULL UNIQUE REFERENCES parking_sites(id),
    payload jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);
COMMIT;
