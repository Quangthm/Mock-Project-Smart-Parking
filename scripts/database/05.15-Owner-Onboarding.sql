-- UserService database; apply after 05.14, before deploying the new binaries.
BEGIN;
ALTER TABLE owner_applications ADD COLUMN IF NOT EXISTS lot_setup jsonb;
ALTER TABLE owner_applications ADD COLUMN IF NOT EXISTS setup_token_hash text;
ALTER TABLE owner_applications ADD COLUMN IF NOT EXISTS setup_token_expires_at timestamptz;
ALTER TABLE owner_applications ADD COLUMN IF NOT EXISTS approval_requested_by uuid REFERENCES users(id);
ALTER TABLE users ADD COLUMN IF NOT EXISTS onboarding_complete boolean NOT NULL DEFAULT false;
-- Legacy pending applications deliberately remain incomplete; no invented lot data.
COMMIT;
