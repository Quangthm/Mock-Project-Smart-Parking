-- ======================================================================================
-- MICROSERVICE: NOTIFICATION & AUDIT SERVICE DATABASE (NotificationAuditDb)
-- Domain: Multi-channel Durable Notifications, Template Management, Push Tokens,
--         Central Audit Projections, General Support & Account Recovery Intake Module
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG MẪU THÔNG BÁO (NOTIFICATION TEMPLATES)
CREATE TABLE notification_templates (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    template_code VARCHAR(100) NOT NULL,
    version INT NOT NULL DEFAULT 1,
    channel VARCHAR(50) NOT NULL CHECK (channel IN ('IN_APP', 'EMAIL', 'SMS', 'PUSH')),
    subject VARCHAR(255),
    body_content TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(template_code, version)
);
CREATE INDEX idx_notification_templates_code ON notification_templates(template_code, is_active);

-- 2. BẢNG THIẾT BỊ NHẬN PUSH NOTIFICATION (PUSH DEVICE TOKENS)
CREATE TABLE push_device_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL, -- Logical Reference to UserDb
    device_token VARCHAR(255) NOT NULL,
    device_os VARCHAR(50) CHECK (device_os IN ('IOS', 'ANDROID', 'WEB')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_used_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (user_id, device_token)
);
CREATE INDEX idx_push_device_tokens_user ON push_device_tokens(user_id);

-- 3. BẢNG CHUYỂN PHÁT THÔNG BÁO BỀN VỮNG (DURABLE NOTIFICATIONS)
-- Quản lý vòng đời gửi thông báo từ RabbitMQ consumer: retry, deduplication, log lỗi
CREATE TABLE durable_notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    originating_event VARCHAR(100) NOT NULL,
    idempotency_key VARCHAR(255) NOT NULL UNIQUE,
    recipient_id UUID NOT NULL, -- Logical ID user/operator
    channel VARCHAR(50) NOT NULL CHECK (channel IN ('IN_APP', 'EMAIL', 'SMS', 'PUSH')),
    template_code VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PROCESSING', 'DELIVERED', 'FAILED', 'DEAD_LETTER')),
    attempts INT NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    max_attempts INT NOT NULL DEFAULT 3,
    last_error TEXT NULL,
    scheduled_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    delivered_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_durable_notifications_recipient ON durable_notifications(recipient_id, status, created_at DESC);
CREATE INDEX idx_durable_notifications_dispatch ON durable_notifications(status, scheduled_at) WHERE status IN ('PENDING', 'PROCESSING');

-- 4. BẢNG TIẾP NHẬN HỖ TRỢ CHUNG & ACCOUNT RECOVERY (SUPPORT TICKETS)
-- Tiếp nhận từ FacilityDb sang làm trung tâm hỗ trợ người dùng theo kiến trúc v1.0
CREATE TABLE support_tickets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ticket_number VARCHAR(100) NOT NULL UNIQUE,
    tenant_id UUID NULL,
    site_id UUID NULL,
    requester_id UUID NOT NULL, -- Logical ID người yêu cầu hỗ trợ
    category VARCHAR(50) NOT NULL CHECK (category IN ('GENERAL_INQUIRY', 'PAYMENT_DISPUTE', 'ACCOUNT_RECOVERY', 'FACILITY_COMPLAINT', 'SYSTEM_BUG')),
    subject VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    priority VARCHAR(20) NOT NULL DEFAULT 'MEDIUM' CHECK (priority IN ('LOW', 'MEDIUM', 'HIGH', 'URGENT')),
    status VARCHAR(50) NOT NULL DEFAULT 'OPEN' CHECK (status IN ('OPEN', 'IN_PROGRESS', 'WAITING_USER', 'RESOLVED', 'CLOSED')),
    assigned_to UUID NULL, -- Operator hoặc Admin ID
    resolution_notes TEXT NULL,
    resolved_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_support_tickets_requester ON support_tickets(requester_id, status);
CREATE INDEX idx_support_tickets_site ON support_tickets(site_id, status);
CREATE INDEX idx_support_tickets_category ON support_tickets(category, status);

-- 5. BẢNG TIN NHẮN TRAO ĐỔI HỖ TRỢ (SUPPORT TICKET MESSAGES)
CREATE TABLE support_ticket_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ticket_id UUID NOT NULL REFERENCES support_tickets(id) ON DELETE CASCADE,
    sender_id UUID NOT NULL,
    sender_role VARCHAR(50) NOT NULL,
    message TEXT NOT NULL,
    attachments JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_ticket_messages_ticket ON support_ticket_messages(ticket_id, created_at);

-- 6. HỒ CHIẾU KIỂM TOÁN TẬP TRUNG (CENTRAL AUDIT PROJECTIONS - ZERO CASCADE)
-- Tiếp nhận projection log kiểm toán từ toàn bộ 5 service khác qua RabbitMQ.
-- Bảng này thuần túy append-only, phục vụ kiểm toán và an ninh thông tin.
CREATE TABLE central_audit_projections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    originating_service VARCHAR(100) NOT NULL,
    event_id UUID NOT NULL UNIQUE,
    actor_id UUID NOT NULL,
    actor_role VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id UUID NOT NULL,
    details JSONB NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    projected_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_central_audit_actor ON central_audit_projections(actor_id, occurred_at DESC);
CREATE INDEX idx_central_audit_resource ON central_audit_projections(resource_type, resource_id);
CREATE INDEX idx_central_audit_service_time ON central_audit_projections(originating_service, occurred_at DESC);
