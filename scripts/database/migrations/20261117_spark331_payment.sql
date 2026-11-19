-- ======================================================================================
-- SPARK-331: Standalone Idempotent Migration for PaymentDb (smartpark_payment)
-- Target Tables: subscription_financial_terms, subscription_financial_term_revisions,
--                subscription_closure_compensations
-- ======================================================================================
BEGIN;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS subscription_financial_terms (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    plan_id UUID NOT NULL,
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    active_revision INT NOT NULL DEFAULT 1 CHECK (active_revision >= 1),
    currency VARCHAR(10) NOT NULL DEFAULT 'VND',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (site_id, plan_id)
);

CREATE INDEX IF NOT EXISTS idx_sub_fin_terms_site
    ON subscription_financial_terms(site_id, is_active);

CREATE TABLE IF NOT EXISTS subscription_financial_term_revisions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    financial_term_id UUID NOT NULL REFERENCES subscription_financial_terms(id) ON DELETE RESTRICT,
    plan_id UUID NOT NULL,
    plan_revision INT NOT NULL CHECK (plan_revision >= 1),
    revision_number INT NOT NULL CHECK (revision_number >= 1),
    vehicle_prices JSONB NOT NULL,
    refund_policy JSONB NOT NULL,
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_to TIMESTAMPTZ NULL,
    idempotency_key VARCHAR(128) NULL,
    idempotency_payload_hash VARCHAR(128) NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (financial_term_id, revision_number)
);

ALTER TABLE subscription_financial_term_revisions
    ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(128) NULL;
ALTER TABLE subscription_financial_term_revisions
    ADD COLUMN IF NOT EXISTS idempotency_payload_hash VARCHAR(128) NULL;

CREATE INDEX IF NOT EXISTS idx_sub_fin_rev_plan
    ON subscription_financial_term_revisions(plan_id, plan_revision, effective_from DESC);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sub_fin_rev_idempotency
    ON subscription_financial_term_revisions(financial_term_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE TABLE IF NOT EXISTS subscription_closure_compensations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    closure_event_id UUID NOT NULL,
    closure_type VARCHAR(20) NOT NULL CHECK (closure_type IN ('TEMPORARY', 'PERMANENT')),
    pass_id UUID NOT NULL,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    refund_request_id UUID NULL REFERENCES refund_requests(id) ON DELETE RESTRICT,
    compensated_from TIMESTAMPTZ NULL,
    compensated_to TIMESTAMPTZ NULL,
    compensated_intervals JSONB NOT NULL DEFAULT '[]'::jsonb,
    refund_amount DECIMAL(14,2) NOT NULL CHECK (refund_amount >= 0),
    idempotency_key VARCHAR(128) NOT NULL UNIQUE,
    idempotency_payload_hash VARCHAR(128) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (
        (refund_amount = 0 AND compensated_from IS NULL AND compensated_to IS NULL)
        OR (compensated_from IS NOT NULL AND compensated_to IS NOT NULL AND compensated_to > compensated_from)
    )
);

ALTER TABLE subscription_closure_compensations
    ADD COLUMN IF NOT EXISTS compensated_intervals JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE subscription_closure_compensations
    ADD COLUMN IF NOT EXISTS idempotency_payload_hash VARCHAR(128) NOT NULL DEFAULT '';
ALTER TABLE subscription_closure_compensations
    ALTER COLUMN compensated_from DROP NOT NULL;
ALTER TABLE subscription_closure_compensations
    ALTER COLUMN compensated_to DROP NOT NULL;

CREATE INDEX IF NOT EXISTS idx_sub_closure_comp_order
    ON subscription_closure_compensations(payment_order_id, created_at);

CREATE INDEX IF NOT EXISTS idx_sub_closure_comp_pass
    ON subscription_closure_compensations(pass_id);

COMMIT;
