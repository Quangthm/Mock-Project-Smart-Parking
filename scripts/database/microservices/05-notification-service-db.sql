-- ======================================================================================
-- MICROSERVICE: NOTIFICATION SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG MẪU THÔNG BÁO (TEMPLATES)
CREATE TABLE notification_templates (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    template_code VARCHAR(100) NOT NULL, -- vd: 'PAYMENT_SUCCESS_EMAIL'
    version INT NOT NULL DEFAULT 1,
    channel VARCHAR(50) NOT NULL CHECK (channel IN ('EMAIL', 'SMS', 'PUSH')), -- 'EMAIL', 'SMS', 'PUSH'
    subject VARCHAR(255),
    body_content TEXT NOT NULL, -- Hỗ trợ placeholder kiểu {{user_name}}
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(template_code, version)
);

CREATE INDEX idx_notification_templates_code ON notification_templates(template_code);

-- 2. BẢNG THIẾT BỊ NHẬN PUSH NOTIFICATION (PUSH TOKENS)
CREATE TABLE push_device_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Cross-domain References (Logical ID)
    user_id UUID NOT NULL,
    
    device_token VARCHAR(255) NOT NULL, -- FCM Token / APNS Token
    device_os VARCHAR(50) CHECK (device_os IN ('IOS', 'ANDROID', 'WEB')), -- 'IOS', 'ANDROID', 'WEB'
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_used_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_push_device_tokens_user_id ON push_device_tokens(user_id);
CREATE INDEX idx_push_device_tokens_device_token ON push_device_tokens(device_token);

-- 3. BẢNG LỊCH SỬ GỬI THÔNG BÁO (NOTIFICATION LOGS)
CREATE TABLE notification_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Cross-domain References
    target_user_id UUID,
    event_id VARCHAR(100), -- To record authoritative business event ID per FR-RPT-01
    
    template_code VARCHAR(100),
    template_version INT,
    
    channel VARCHAR(50) NOT NULL CHECK (channel IN ('EMAIL', 'SMS', 'PUSH')), -- 'EMAIL', 'SMS', 'PUSH'
    recipient VARCHAR(255) NOT NULL, -- Email address, SĐT, hoặc Push Token
    subject VARCHAR(255),
    content TEXT,
    
    attempt_count INT NOT NULL DEFAULT 1 CHECK (attempt_count >= 1),
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SENT', 'FAILED')), -- 'PENDING', 'SENT', 'FAILED'
    failure_reason TEXT, -- renamed from error_message per FR-RPT-01
    sent_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_notification_event_recipient_channel UNIQUE(event_id, recipient, channel)
);

CREATE INDEX idx_notification_logs_target_user_id ON notification_logs(target_user_id);
CREATE INDEX idx_notification_logs_status ON notification_logs(status);
CREATE INDEX idx_notification_logs_event_id ON notification_logs(event_id);
CREATE INDEX idx_notification_logs_created_at ON notification_logs(created_at);
