-- ======================================================================================
-- MICROSERVICE: PAYMENT & PRICING SERVICE DATABASE (PaymentDb)
-- Domain: Dynamic Tariffs, Immutable Policy Revisions, Payment Orders, Transactions,
--         payOS Gateway Integration, Cash Reconciliation, Invoices, Financial Refunds
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG BIỂU GIÁ BÃI XE (TARIFFS)
-- Tiếp nhận từ FacilityDb sang Payment & Pricing Service theo đúng Bounded Context v1.0
CREATE TABLE tariffs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    free_parking_grace_minutes INT NOT NULL DEFAULT 15 CHECK (free_parking_grace_minutes >= 0),
    tariff_rules JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_tariffs_site_vehicle ON tariffs(site_id, vehicle_type) WHERE is_active = TRUE AND deleted_at IS NULL;

-- 2. BẢNG LỊCH SỬ CHÍNH SÁCH BẤT BIẾN (POLICY REVISIONS - IMMUTABLE AUDIT)
-- Giải quyết triệt để lỗi ghi đè version cũ. Mọi sửa đổi biểu giá đều sinh bản ghi mới,
-- tuyệt đối cấm UPDATE bản ghi cũ để bảo toàn lịch sử đối soát tài chính.
CREATE TABLE policy_revisions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    policy_id UUID NOT NULL, -- ID gốc của tariff hoặc cấu hình chính sách
    policy_type VARCHAR(50) NOT NULL CHECK (policy_type IN ('TARIFF', 'SUBSCRIPTION', 'OVERSTAY_PENALTY', 'SPECIAL_EVENT')),
    revision_number INT NOT NULL CHECK (revision_number >= 1),
    effective_from TIMESTAMPTZ NOT NULL,
    effective_to TIMESTAMPTZ NULL,
    scope_type VARCHAR(50) NOT NULL DEFAULT 'SITE' CHECK (scope_type IN ('GLOBAL', 'TENANT', 'SITE', 'ZONE')),
    scope_id UUID,
    policy_values JSONB NOT NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (policy_id, revision_number)
);
CREATE INDEX idx_policy_revisions_lookup ON policy_revisions(policy_id, effective_from);
CREATE INDEX idx_policy_revisions_scope ON policy_revisions(scope_type, scope_id);

-- 3. BẢNG ĐƠN HÀNG THANH TOÁN (PAYMENT ORDERS)
CREATE TABLE payment_orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID,
    
    order_code VARCHAR(100) NOT NULL UNIQUE,
    idempotency_key VARCHAR(255) UNIQUE,
    order_type VARCHAR(50) NOT NULL CHECK (order_type IN ('PARKING_FEE', 'MONTHLY_PASS_FEE', 'OVERNIGHT_FEE', 'RESERVATION_FEE')), 
    reference_type VARCHAR(50) NOT NULL CHECK (reference_type IN ('RESERVATION', 'PARKING_SESSION', 'MONTHLY_PASS')),
    reference_id UUID NOT NULL, -- Logical ID trỏ sang OperationsDb
    
    amount DECIMAL(14,2) NOT NULL CHECK (amount >= 0),
    discount_amount DECIMAL(14,2) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    deposit_deducted DECIMAL(14,2) NOT NULL DEFAULT 0 CHECK (deposit_deducted >= 0),
    final_amount DECIMAL(14,2) NOT NULL CHECK (final_amount >= 0),
    CHECK (final_amount = (amount - discount_amount - deposit_deducted)),
    
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED')),
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_payment_orders_tenant_site ON payment_orders(tenant_id, site_id);
CREATE INDEX idx_payment_orders_account ON payment_orders(account_id);
CREATE INDEX idx_payment_orders_reference ON payment_orders(reference_type, reference_id);
CREATE INDEX idx_payment_orders_status ON payment_orders(status);

-- 4. BẢNG GIAO DỊCH DÒNG TIỀN (PAYMENT TRANSACTIONS)
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    
    transaction_type VARCHAR(50) NOT NULL CHECK (transaction_type IN ('PAYMENT', 'REFUND')),
    payment_method VARCHAR(50) NOT NULL CHECK (payment_method IN ('PAYOS', 'VNPAY', 'MOMO', 'ZALOPAY', 'CASH')),
    gateway_transaction_id VARCHAR(255),
    amount DECIMAL(14,2) NOT NULL,
    
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'UNKNOWN')),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_payment_transactions_order ON payment_transactions(payment_order_id);
CREATE UNIQUE INDEX idx_provider_transaction_unique ON payment_transactions(payment_method, gateway_transaction_id) 
    WHERE gateway_transaction_id IS NOT NULL;

-- 5. BẢNG HÓA ĐƠN KẾ TOÁN (INVOICES - HỖ TRỢ CẢ PHIÊN ĐỖ VÀ VÉ THÁNG/ĐẶT CHỖ)
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    
    site_id UUID NOT NULL,
    reference_type VARCHAR(50) NOT NULL CHECK (reference_type IN ('PARKING_SESSION', 'RESERVATION', 'MONTHLY_PASS')),
    reference_id UUID NOT NULL,
    session_id UUID NULL, -- Giữ để tương thích ngược nếu là phiên đỗ
    
    invoice_number VARCHAR(100) NOT NULL UNIQUE,
    total_parked_minutes INT NOT NULL DEFAULT 0,
    base_parking_fee DECIMAL(14,2) NOT NULL DEFAULT 0,
    overstay_penalty_fee DECIMAL(14,2) NOT NULL DEFAULT 0,
    final_amount DECIMAL(14,2) NOT NULL,
    pricing_snapshot JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_invoices_site ON invoices(site_id);
CREATE INDEX idx_invoices_order ON invoices(payment_order_id);
CREATE INDEX idx_invoices_reference ON invoices(reference_type, reference_id);

-- 6. BẢNG YÊU CẦU HOÀN TIỀN (REFUND REQUESTS)
CREATE TABLE refund_requests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    
    amount DECIMAL(14,2) NOT NULL CHECK (amount > 0),
    reason TEXT NOT NULL,
    evidence_url VARCHAR(500),
    
    status VARCHAR(50) NOT NULL DEFAULT 'REFUND_REQUESTED' 
        CHECK (status IN ('REFUND_REQUESTED', 'REFUND_APPROVED', 'REFUND_SUBMITTED', 'REFUND_COMPLETED', 'REJECTED')),
    approved_by UUID, -- Operator hoặc Owner ID
    handled_by_owner BOOLEAN DEFAULT FALSE,
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_refund_requests_order ON refund_requests(payment_order_id);
CREATE INDEX idx_refund_requests_status ON refund_requests(status);

-- 7. BẢNG HOOK NHÀ CUNG CẤP CỔNG THANH TOÁN (PROVIDER WEBHOOKS)
CREATE TABLE provider_webhooks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    provider VARCHAR(50) NOT NULL,
    provider_event_id VARCHAR(255) NOT NULL,
    processing_state VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (processing_state IN ('PENDING', 'PROCESSED', 'FAILED')),
    payload JSONB,
    received_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (provider, provider_event_id)
);
CREATE INDEX idx_provider_webhooks_state ON provider_webhooks(processing_state, received_at);
