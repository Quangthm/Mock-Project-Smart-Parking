-- ======================================================================================
-- SPARK-331: Standalone Idempotent Migration for OperationsDb (smartpark_operations)
-- Target Tables: parking_pass_plans, parking_pass_plan_revisions
-- Note: DB28-PASS-EXT (monthly_passes alterations & SCHEDULED) remains deferred to SPARK-332.
-- ======================================================================================
BEGIN;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS parking_pass_plans (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    entitlement_type VARCHAR(50) NOT NULL DEFAULT 'WHEN_SPACE_AVAILABLE'
        CHECK (entitlement_type = 'WHEN_SPACE_AVAILABLE'),
    is_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(30) NOT NULL DEFAULT 'DRAFT'
        CHECK (status IN ('DRAFT', 'PUBLISHED', 'DISABLED')),
    active_revision INT NOT NULL DEFAULT 0 CHECK (active_revision >= 0),
    draft_revision INT NULL,
    draft_payload JSONB NULL,
    created_by UUID NOT NULL,
    updated_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (site_id, product_type)
);

CREATE INDEX IF NOT EXISTS idx_parking_pass_plans_site
    ON parking_pass_plans(site_id, is_enabled);

CREATE TABLE IF NOT EXISTS parking_pass_plan_revisions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    plan_id UUID NOT NULL REFERENCES parking_pass_plans(id) ON DELETE RESTRICT,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    revision_number INT NOT NULL CHECK (revision_number >= 1),
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    zone_id UUID NULL,
    vehicle_types JSONB NOT NULL,
    ordinary_coverage_rules JSONB NOT NULL,
    contract_timezone VARCHAR(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_to TIMESTAMPTZ NULL,
    publish_note TEXT NULL,
    idempotency_key VARCHAR(128) NULL,
    idempotency_payload_hash VARCHAR(128) NULL,
    published_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (plan_id, revision_number)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_pass_plan_rev_idempotency
    ON parking_pass_plan_revisions(plan_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_pass_plan_rev_site
    ON parking_pass_plan_revisions(site_id, product_type, revision_number DESC);

COMMIT;
