-- Apply once to an existing SmartPark database before starting the updated UserService.
-- The baseline initialization script already contains these fields for new databases.
BEGIN;
ALTER TABLE users ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER NOT NULL DEFAULT 0;
ALTER TABLE users ADD COLUMN IF NOT EXISTS locked_until TIMESTAMPTZ;
ALTER TABLE user_refresh_tokens ADD COLUMN IF NOT EXISTS access_token_id UUID;
ALTER TABLE user_refresh_tokens ADD COLUMN IF NOT EXISTS access_expires_at TIMESTAMPTZ;
-- Existing refresh rows have no valid access session. Preserve them as revoked history.
UPDATE user_refresh_tokens SET is_revoked = true,
    access_token_id = COALESCE(access_token_id, id),
    access_expires_at = COALESCE(access_expires_at, created_at)
WHERE access_token_id IS NULL OR access_expires_at IS NULL;
ALTER TABLE user_refresh_tokens ALTER COLUMN access_token_id SET NOT NULL;
ALTER TABLE user_refresh_tokens ALTER COLUMN access_expires_at SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_refresh_access_token_id ON user_refresh_tokens(access_token_id);
COMMIT;
