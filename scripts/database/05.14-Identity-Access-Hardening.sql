BEGIN;
ALTER TABLE users ADD COLUMN IF NOT EXISTS access_version BIGINT NOT NULL DEFAULT 0;
ALTER TABLE user_refresh_tokens ADD COLUMN IF NOT EXISTS last_refreshed_at TIMESTAMPTZ;
-- Legacy rows have no trustworthy refresh timestamp. Start from issuance, never deployment time.
UPDATE user_refresh_tokens SET last_refreshed_at = created_at WHERE last_refreshed_at IS NULL;
-- Shorten legacy sessions conservatively; never extend access or absolute lifetime.
UPDATE user_refresh_tokens SET expires_at = LEAST(expires_at, created_at + INTERVAL '7 days'),
    access_expires_at = LEAST(access_expires_at, COALESCE(last_refreshed_at, created_at) + INTERVAL '15 minutes');
ALTER TABLE accounts DROP CONSTRAINT IF EXISTS accounts_status_check;
ALTER TABLE accounts ADD CONSTRAINT accounts_status_check CHECK
    (status IN ('ACTIVE','INACTIVE','LOCKED','PENDING_APPROVAL','PENDING_VERIFICATION'));
-- Never infer verified contact for historical Operators. Preserve blocked and multi-role users.
UPDATE users u SET status='PENDING_VERIFICATION'
WHERE u.status='ACTIVE' AND u.email_verified_at IS NULL AND u.phone_verified_at IS NULL AND u.deleted_at IS NULL
  AND EXISTS (SELECT 1 FROM accounts a JOIN account_roles r ON r.account_id=a.id
    WHERE a.user_id=u.id AND a.deleted_at IS NULL AND r.role_code='SITE_OPERATOR')
  AND NOT EXISTS (SELECT 1 FROM accounts a JOIN account_roles r ON r.account_id=a.id
    WHERE a.user_id=u.id AND a.deleted_at IS NULL AND r.role_code<>'SITE_OPERATOR');
UPDATE accounts a SET status='PENDING_VERIFICATION' FROM users u
WHERE a.user_id=u.id AND u.status='PENDING_VERIFICATION' AND a.status='ACTIVE'
  AND EXISTS (SELECT 1 FROM account_roles r WHERE r.account_id=a.id AND r.role_code='SITE_OPERATOR');
CREATE TABLE IF NOT EXISTS access_event_outbox (
    id UUID PRIMARY KEY, payload TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL,
    published_at TIMESTAMPTZ, lease_id UUID, lease_until TIMESTAMPTZ);
CREATE INDEX IF NOT EXISTS ix_access_event_outbox_pending ON access_event_outbox(created_at) WHERE published_at IS NULL;
CREATE TABLE IF NOT EXISTS access_event_inbox (id UUID PRIMARY KEY, received_at TIMESTAMPTZ NOT NULL);
CREATE TABLE IF NOT EXISTS access_projection (user_id UUID PRIMARY KEY, version BIGINT NOT NULL, status TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS rotated_session_proofs (
    access_token_id UUID PRIMARY KEY, session_id UUID NOT NULL REFERENCES user_refresh_tokens(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id), token_hash VARCHAR(255) NOT NULL, access_expires_at TIMESTAMPTZ NOT NULL);
COMMIT;
