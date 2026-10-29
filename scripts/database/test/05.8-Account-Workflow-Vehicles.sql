-- UserService only. Apply after the service alignment overlay. Idempotent.
BEGIN;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS permissions TEXT[];
ALTER TABLE users ADD COLUMN IF NOT EXISTS email_verified_at TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN IF NOT EXISTS phone_verified_at TIMESTAMPTZ;
-- Add the baseline-confirmed explicit operational override grant to the existing allowlist.
ALTER TABLE operator_grants DROP CONSTRAINT IF EXISTS operator_grants_permissions_check;
ALTER TABLE operator_grants DROP CONSTRAINT IF EXISTS operator_grants_permissions_check1;
ALTER TABLE operator_grants DROP CONSTRAINT IF EXISTS operator_grants_permissions_check2;
ALTER TABLE operator_grants ADD CONSTRAINT operator_grants_permissions_check CHECK(cardinality(permissions) BETWEEN 1 AND 5);
ALTER TABLE operator_grants ADD CONSTRAINT operator_grants_permissions_check1 CHECK(array_position(permissions,NULL) IS NULL);
ALTER TABLE operator_grants ADD CONSTRAINT operator_grants_permissions_check2 CHECK(permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW','SLOT_OVERRIDE']::text[]);
-- Existing uniqueness constraints abort the entire migration on conflicting contacts.
-- Never silently merge accounts or infer verification for historical data.
UPDATE users SET email=lower(btrim(email)),phone=CASE
 WHEN btrim(phone) LIKE '+84%' THEN '0'||substring(btrim(phone) FROM 4)
 WHEN btrim(phone) LIKE '84%' AND length(btrim(phone))=11 THEN '0'||substring(btrim(phone) FROM 3)
 ELSE btrim(phone) END WHERE deleted_at IS NULL;
CREATE TABLE IF NOT EXISTS auth_challenges (
 id UUID PRIMARY KEY, user_id UUID NOT NULL REFERENCES users(id), purpose TEXT NOT NULL,
 channel TEXT NOT NULL, destination TEXT NOT NULL, code_hash TEXT NOT NULL,
 expires_at TIMESTAMPTZ NOT NULL, resend_at TIMESTAMPTZ NOT NULL,
 attempts INT NOT NULL DEFAULT 0, locked_until TIMESTAMPTZ, consumed_at TIMESTAMPTZ);
CREATE INDEX IF NOT EXISTS ix_auth_challenge_user ON auth_challenges(user_id,purpose);
CREATE TABLE IF NOT EXISTS workflow_deliveries (
 id UUID PRIMARY KEY, user_id UUID NOT NULL REFERENCES users(id), actor_id UUID NOT NULL REFERENCES users(id),
 kind TEXT NOT NULL, protected_payload TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'pending',
 attempts INT NOT NULL DEFAULT 0, created_at TIMESTAMPTZ NOT NULL, sent_at TIMESTAMPTZ,
 retry_at TIMESTAMPTZ, challenge_id UUID REFERENCES auth_challenges(id),
 CHECK(status IN ('pending','failed','sent','cancelled')));
CREATE INDEX IF NOT EXISTS ix_workflow_delivery_due ON workflow_deliveries(status,retry_at);
ALTER TABLE workflow_deliveries ADD COLUMN IF NOT EXISTS expires_at TIMESTAMPTZ;
CREATE TABLE IF NOT EXISTS mfa_credentials (
 user_id UUID PRIMARY KEY REFERENCES users(id), protected_secret TEXT NOT NULL, enabled BOOL NOT NULL DEFAULT FALSE,
 last_step BIGINT NOT NULL DEFAULT -1, attempts INT NOT NULL DEFAULT 0, locked_until TIMESTAMPTZ);
-- Reuse the canonical account-owned vehicles table and preserve historical IDs.
ALTER TABLE vehicles ALTER COLUMN original_plate TYPE VARCHAR(30);
-- Schema decision VEH-OPEN: global plate binding cannot implement the OPEN FR-VEH-02.
-- Records do not establish legal ownership; no claim/transfer workflow is added.
DROP INDEX IF EXISTS uq_active_vehicle_plate;
CREATE INDEX IF NOT EXISTS ix_active_vehicle_plate ON vehicles(normalized_plate) WHERE deleted_at IS NULL;
COMMIT;
