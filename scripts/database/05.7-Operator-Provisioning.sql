-- SPARK-192. Apply after 05.1, 05.4 (nullable phone + normalized email uniqueness).
BEGIN;
INSERT INTO roles(code, name) VALUES ('SITE_OPERATOR', 'Site Operator / Attendant') ON CONFLICT (code) DO NOTHING;
CREATE TABLE IF NOT EXISTS operator_grants (
    account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    permissions TEXT[] NOT NULL,
    CHECK (cardinality(permissions) BETWEEN 1 AND 4),
    CHECK (array_position(permissions, NULL) IS NULL),
    CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW']::TEXT[])
);
CREATE INDEX IF NOT EXISTS idx_operator_grants_creator ON operator_grants(created_by);
COMMIT;
