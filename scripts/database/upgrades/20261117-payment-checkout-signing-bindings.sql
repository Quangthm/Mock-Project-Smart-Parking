-- Approved SPARK-340 F6/F7 additive Schema Freeze exception.
-- Review and apply explicitly to Payment DB before deploying the new repository.
-- No startup migration, no policy-history deletion, no financial-state mutation.
BEGIN;
CREATE TABLE IF NOT EXISTS checkout_signing_bindings (
    order_code BIGINT PRIMARY KEY CHECK (order_code > 0),
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    protected_binding TEXT NOT NULL CHECK (length(protected_binding) > 0),
    source VARCHAR(24) NOT NULL CHECK (source IN ('CHECKOUT', 'LEGACY_IMPORT', 'LEGACY_POLICY')),
    evidence_reference VARCHAR(256),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_checkout_signing_binding_order ON checkout_signing_bindings(payment_order_id);
COMMENT ON TABLE checkout_signing_bindings IS
    'Immutable protected checkout verification metadata. Retain with financial history; no tariff pruning or automatic TTL.';
COMMIT;
