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

-- 2.1 DỮ LIỆU MẪU CHO MÔI TRƯỜNG DEV
-- Đồng bộ với SmartPark Demo Site trong facility-db.
INSERT INTO tariffs (
    id, tenant_id, site_id, name, vehicle_type,
    free_parking_grace_minutes, tariff_rules, is_active
)
VALUES
(
    'c0000000-0000-4000-8000-000000000001',
    'a0000000-0000-4000-8000-000000000001',
    'b0000000-0000-4000-8000-000000000001',
    'SmartPark Demo Car Tariff',
    'CAR',
    15,
    '{
      "timeZoneId": "Asia/Ho_Chi_Minh",
      "roundingMode": "HALF_UP_THOUSAND",
      "timeBlocks": [
        {"blockCode": "DAY_SHIFT", "startTime": "06:00", "endTime": "18:00", "ratePerHourVnd": 20000},
        {"blockCode": "NIGHT_SHIFT", "startTime": "18:00", "endTime": "06:00", "ratePerHourVnd": 30000}
      ]
    }'::jsonb,
    TRUE
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO tariffs (
    id, tenant_id, site_id, name, vehicle_type,
    free_parking_grace_minutes, tariff_rules, is_active
)
VALUES
(
    'c0000000-0000-4000-8000-000000000002',
    'a0000000-0000-4000-8000-000000000001',
    'b0000000-0000-4000-8000-000000000001',
    'SmartPark Demo Motorcycle Tariff',
    'MOTORCYCLE',
    15,
    '{
      "timeZoneId": "Asia/Ho_Chi_Minh",
      "roundingMode": "HALF_UP_THOUSAND",
      "timeBlocks": [
        {"blockCode": "DAY_SHIFT", "startTime": "06:00", "endTime": "18:00", "ratePerHourVnd": 10000},
        {"blockCode": "NIGHT_SHIFT", "startTime": "18:00", "endTime": "06:00", "ratePerHourVnd": 15000}
      ]
    }'::jsonb,
    TRUE
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO policy_revisions (
    id, policy_id, policy_type, revision_number, effective_from,
    scope_type, scope_id, policy_values, created_by
)
SELECT
    'd0000000-0000-4000-8000-000000000001',
    'c0000000-0000-4000-8000-000000000001',
    'TARIFF',
    1,
    CURRENT_TIMESTAMP,
    'SITE',
    'b0000000-0000-4000-8000-000000000001',
    t.tariff_rules,
    'a0000000-0000-4000-8000-000000000001'
FROM tariffs t
WHERE t.id = 'c0000000-0000-4000-8000-000000000001'
ON CONFLICT (id) DO NOTHING;

INSERT INTO policy_revisions (
    id, policy_id, policy_type, revision_number, effective_from,
    scope_type, scope_id, policy_values, created_by
)
SELECT
    'd0000000-0000-4000-8000-000000000002',
    'c0000000-0000-4000-8000-000000000002',
    'TARIFF',
    1,
    CURRENT_TIMESTAMP,
    'SITE',
    'b0000000-0000-4000-8000-000000000001',
    t.tariff_rules,
    'a0000000-0000-4000-8000-000000000001'
FROM tariffs t
WHERE t.id = 'c0000000-0000-4000-8000-000000000002'
ON CONFLICT (id) DO NOTHING;

-- 2.2 BẢNG BÁO GIÁ ĐẶT CHỖ (RESERVATION QUOTES - IMMUTABLE QUOTE SNAPSHOT)
CREATE TABLE reservation_quotes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID NOT NULL,
    vehicle_id UUID,
    requested_slot_id UUID,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    
    booked_amount DECIMAL(14,2) NOT NULL CHECK (booked_amount >= 0),
    deposit_amount DECIMAL(14,2) NOT NULL CHECK (deposit_amount >= 0),
    prepayment_mode VARCHAR(50) NOT NULL DEFAULT 'PERCENTAGE' CHECK (prepayment_mode IN ('FULL', 'PERCENTAGE', 'FIXED')),
    currency VARCHAR(10) NOT NULL DEFAULT 'VND',
    
    tariff_id UUID,
    tariff_revision_id UUID,
    terms_version VARCHAR(50) NOT NULL DEFAULT 'v1',
    coverage_version VARCHAR(100),
    
    valid_until TIMESTAMPTZ NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'ISSUED' CHECK (status IN ('ISSUED', 'ACCEPTED', 'EXPIRED', 'CANCELLED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_reservation_quotes_account ON reservation_quotes(account_id);
CREATE INDEX idx_reservation_quotes_site_status ON reservation_quotes(site_id, status);
CREATE INDEX idx_reservation_quotes_validity ON reservation_quotes(valid_until);

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
    plate_number VARCHAR(50),
    
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
-- Checkout is created before the gate transition. The order remains durable while
-- the operator waits in payOS or refreshes the browser.
CREATE INDEX idx_payment_orders_pending_checkout
    ON payment_orders(site_id, status, expires_at)
    WHERE status = 'PENDING';

-- 4. BẢNG GIAO DỊCH DÒNG TIỀN (PAYMENT TRANSACTIONS)
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    
    transaction_type VARCHAR(50) NOT NULL CHECK (transaction_type IN ('PAYMENT', 'REFUND')),
    payment_method VARCHAR(50) NOT NULL CHECK (payment_method IN ('PAYOS', 'VNPAY', 'MOMO', 'ZALOPAY', 'CASH')),
    gateway_transaction_id VARCHAR(255),
    idempotency_key VARCHAR(255),
    amount DECIMAL(14,2) NOT NULL,
    
    checkout_url VARCHAR(1000),
    qr_code TEXT,
    account_number VARCHAR(50),
    account_name VARCHAR(100),
    bin VARCHAR(20),
    
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'UNKNOWN')),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_payment_transactions_order ON payment_transactions(payment_order_id);
CREATE INDEX idx_payment_transactions_idempotency ON payment_transactions(idempotency_key);
CREATE UNIQUE INDEX idx_provider_transaction_unique ON payment_transactions(payment_method, gateway_transaction_id) 
    WHERE gateway_transaction_id IS NOT NULL;
-- A successful transaction is the durable prerequisite for OperationService
-- to complete a parking-session check-out. CASH and demo-payOS use this table too.
CREATE INDEX idx_payment_transactions_pending_reconciliation
    ON payment_transactions(payment_order_id, status)
    WHERE status IN ('PENDING', 'UNKNOWN');

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

-- 8. BẢNG TIN TRANSACTIONAL OUTBOX (MASSTRANSIT EF CORE OUTBOX - POSTGRESQL NATIVE)
CREATE TABLE IF NOT EXISTS "InboxState" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MessageId" uuid NOT NULL,
    "ConsumerId" uuid NOT NULL,
    "LockId" uuid NOT NULL,
    "RowVersion" bytea,
    "Received" timestamp with time zone NOT NULL,
    "ReceiveCount" integer NOT NULL,
    "ExpirationTime" timestamp with time zone,
    "Consumed" timestamp with time zone,
    "Delivered" timestamp with time zone,
    "LastSequenceNumber" bigint,
    CONSTRAINT "PK_InboxState" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_InboxState_MessageId_ConsumerId" UNIQUE ("MessageId", "ConsumerId")
);

CREATE TABLE IF NOT EXISTS "OutboxState" (
    "OutboxId" uuid NOT NULL,
    "LockId" uuid NOT NULL,
    "RowVersion" bytea,
    "Created" timestamp with time zone NOT NULL,
    "Delivered" timestamp with time zone,
    "LastSequenceNumber" bigint,
    CONSTRAINT "PK_OutboxState" PRIMARY KEY ("OutboxId")
);

CREATE TABLE IF NOT EXISTS "OutboxMessage" (
    "SequenceNumber" bigint GENERATED BY DEFAULT AS IDENTITY,
    "EnqueueTime" timestamp with time zone,
    "SentTime" timestamp with time zone NOT NULL,
    "Headers" text,
    "Properties" text,
    "InboxMessageId" uuid,
    "InboxConsumerId" uuid,
    "OutboxId" uuid,
    "MessageId" uuid NOT NULL,
    "ContentType" character varying(256) NOT NULL,
    "MessageType" text NOT NULL,
    "Body" text NOT NULL,
    "ConversationId" uuid,
    "CorrelationId" uuid,
    "InitiatorId" uuid,
    "RequestId" uuid,
    "SourceAddress" character varying(256),
    "DestinationAddress" character varying(256),
    "ResponseAddress" character varying(256),
    "FaultAddress" character varying(256),
    "ExpirationTime" timestamp with time zone,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("SequenceNumber"),
    CONSTRAINT "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId" FOREIGN KEY ("InboxMessageId", "InboxConsumerId") REFERENCES "InboxState" ("MessageId", "ConsumerId"),
    CONSTRAINT "FK_OutboxMessage_OutboxState_OutboxId" FOREIGN KEY ("OutboxId") REFERENCES "OutboxState" ("OutboxId")
);

CREATE INDEX IF NOT EXISTS "IX_InboxState_Delivered" ON "InboxState" ("Delivered");
CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_EnqueueTime" ON "OutboxMessage" ("EnqueueTime");
CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_ExpirationTime" ON "OutboxMessage" ("ExpirationTime");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber" ON "OutboxMessage" ("InboxMessageId", "InboxConsumerId", "SequenceNumber");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OutboxMessage_OutboxId_SequenceNumber" ON "OutboxMessage" ("OutboxId", "SequenceNumber");
CREATE INDEX IF NOT EXISTS "IX_OutboxState_Created" ON "OutboxState" ("Created");
