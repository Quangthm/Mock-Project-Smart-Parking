-- Apply after 05.2 and 05.4. Re-runnable; no schema mutation at app startup.
BEGIN;
INSERT INTO roles(code, name) VALUES ('BUSINESS_OWNER', 'Parking Business Owner') ON CONFLICT (code) DO NOTHING;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_user_email_normalized
    ON users(lower(email)) WHERE deleted_at IS NULL AND email IS NOT NULL;
CREATE TABLE IF NOT EXISTS owner_applications (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    business_name VARCHAR(255) NOT NULL,
    lot_type VARCHAR(30) NOT NULL CHECK (lot_type IN ('outdoor', 'basement', 'multi-storey')),
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    submitted_at TIMESTAMPTZ NOT NULL,
    reviewed_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES users(id),
    review_note VARCHAR(2000),
    CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL)
        OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))
);
COMMIT;
