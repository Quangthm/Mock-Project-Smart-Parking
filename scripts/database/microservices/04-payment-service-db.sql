-- ======================================================================================
-- MICROSERVICE: PAYMENT SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG ĐƠN HÀNG THANH TOÁN (PAYMENT ORDERS)
CREATE TABLE payment_orders (
    id UUID DEFAULT uuid_generate_v4(),
    
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID,
    
    order_code VARCHAR(100) NOT NULL UNIQUE,
    idempotency_key VARCHAR(255) UNIQUE,
    order_type VARCHAR(50) NOT NULL CHECK (order_type IN ('PARKING_FEE', 'MONTHLY_PASS_FEE', 'OVERNIGHT_FEE', 'RESERVATION_FEE')), 
    reference_type VARCHAR(50) NOT NULL CHECK (reference_type IN ('RESERVATION', 'PARKING_SESSION')), -- 'RESERVATION', 'PARKING_SESSION'
    reference_id UUID NOT NULL, -- Logical ID trỏ sang DB Reservation
    
    amount DECIMAL(12,2) NOT NULL CHECK (amount >= 0),
    discount_amount DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    deposit_deducted DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (deposit_deducted >= 0),
    final_amount DECIMAL(12,2) NOT NULL CHECK (final_amount >= 0),
    CHECK (final_amount = (amount - discount_amount - deposit_deducted)),
    
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED')),
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    PRIMARY KEY (id),
    UNIQUE (id, tenant_id) -- For RLS Composite FKs
);

-- 2. BẢNG GIAO DỊCH DÒNG TIỀN (TRANSACTIONS)
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    
    transaction_type VARCHAR(50) NOT NULL CHECK (transaction_type IN ('PAYMENT', 'REFUND')), -- 'PAYMENT', 'REFUND'
    payment_method VARCHAR(50) NOT NULL CHECK (payment_method IN ('VNPAY', 'MOMO', 'ZALOPAY', 'CASH')), -- 'VNPAY', 'MOMO', 'ZALOPAY', 'CASH'
    gateway_transaction_id VARCHAR(255),
    amount DECIMAL(12,2) NOT NULL,
    
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'UNKNOWN')),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);

-- 3. BẢNG HÓA ĐƠN KẾ TOÁN (INVOICES)
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    
    -- Cross-domain References (Logical IDs)
    session_id UUID NOT NULL,
    site_id UUID NOT NULL,
    
    invoice_number VARCHAR(100) NOT NULL UNIQUE,
    total_parked_minutes INT NOT NULL,
    base_parking_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    overstay_penalty_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    final_amount DECIMAL(12,2) NOT NULL,
    pricing_snapshot JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);

-- 4. BẢNG YÊU CẦU HOÀN TIỀN (REFUND REQUESTS)
CREATE TABLE refund_requests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    
    amount DECIMAL(12,2) NOT NULL,
    reason TEXT NOT NULL,
    evidence_url VARCHAR(255),
    
    status VARCHAR(50) NOT NULL DEFAULT 'REFUND_REQUESTED' CHECK (status IN ('REFUND_REQUESTED', 'REFUND_APPROVED', 'REFUND_SUBMITTED', 'REFUND_COMPLETED', 'REJECTED')), -- REFUND_REQUESTED, REFUND_APPROVED, REFUND_SUBMITTED, REFUND_COMPLETED, REJECTED
    approved_by UUID, -- Operator or Owner ID
    handled_by_owner BOOLEAN DEFAULT FALSE,
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);

-- 5. BẢNG HOOK NHÀ CUNG CẤP (PROVIDER WEBHOOKS)
CREATE TABLE provider_webhooks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    provider VARCHAR(50) NOT NULL,
    provider_event_id VARCHAR(255) NOT NULL,
    processing_state VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (processing_state IN ('PENDING', 'PROCESSED', 'FAILED')),
    payload JSONB,
    received_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    UNIQUE (provider, provider_event_id)
);

-- ======================================================================================
-- INDEXES FOR PERFORMANCE
-- ======================================================================================
CREATE INDEX idx_payment_orders_tenant_site ON payment_orders(tenant_id, site_id);
CREATE INDEX idx_payment_orders_account ON payment_orders(account_id);
CREATE INDEX idx_payment_orders_reference ON payment_orders(reference_type, reference_id);

CREATE INDEX idx_payment_transactions_order_tenant ON payment_transactions(payment_order_id, tenant_id);
CREATE INDEX idx_payment_transactions_gateway ON payment_transactions(gateway_transaction_id);
CREATE UNIQUE INDEX idx_provider_transaction_unique ON payment_transactions(payment_method, gateway_transaction_id) WHERE gateway_transaction_id IS NOT NULL;

CREATE INDEX idx_invoices_tenant_site ON invoices(tenant_id, site_id);
CREATE INDEX idx_invoices_order_tenant ON invoices(payment_order_id, tenant_id);
CREATE INDEX idx_invoices_session ON invoices(session_id);

CREATE INDEX idx_refund_requests_order_tenant ON refund_requests(payment_order_id, tenant_id);
