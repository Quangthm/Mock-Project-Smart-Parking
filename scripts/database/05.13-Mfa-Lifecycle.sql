-- Apply after 05.8-Account-Workflow-Vehicles.sql. Safe to run repeatedly.
BEGIN;

ALTER TABLE mfa_credentials ADD COLUMN IF NOT EXISTS pending_protected_secret TEXT;
ALTER TABLE mfa_credentials ADD COLUMN IF NOT EXISTS pending_expires_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS mfa_recovery_codes (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    code_hash TEXT NOT NULL,
    used_at TIMESTAMPTZ,
    UNIQUE (user_id, code_hash)
);

COMMIT;
