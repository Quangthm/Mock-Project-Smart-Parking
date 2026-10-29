-- Apply after 05.2. No automatic schema mutation at application startup.
BEGIN;
ALTER TABLE users ALTER COLUMN phone DROP NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_user_email_normalized
    ON users (lower(email)) WHERE deleted_at IS NULL AND email IS NOT NULL;
CREATE TABLE IF NOT EXISTS driver_registrations (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    channel VARCHAR(10) NOT NULL CHECK (channel IN ('email', 'sms')),
    code_hash TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    resend_available_at TIMESTAMPTZ NOT NULL,
    failed_attempts INTEGER NOT NULL DEFAULT 0 CHECK (failed_attempts BETWEEN 0 AND 3),
    locked_until TIMESTAMPTZ,
    verified_at TIMESTAMPTZ
);
COMMIT;
