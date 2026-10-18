-- ======================================================================================
-- MICROSERVICE: IOT & RECOGNITION SERVICE DATABASE (IoTDb)
-- Domain: Edge Devices, Barrier Gates, Raw Telemetries, LPR Evidence & Recognition,
--         AI Assistant Module (Conversations, Messages, Tool Runs)
-- Message Broker: RabbitMQ + MassTransit (Inter-service) & MQTT (IoT Devices)
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG DANH SÁCH THIẾT BỊ NGOẠI VI (DEVICES)
CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    device_code VARCHAR(100) NOT NULL,
    device_type VARCHAR(50) NOT NULL CHECK (device_type IN ('CAMERA_LPR', 'SENSOR', 'BARRIER_GATE')),
    location_desc VARCHAR(255),
    ip_address VARCHAR(45),
    mac_address VARCHAR(45),
    status VARCHAR(50) NOT NULL DEFAULT 'UNKNOWN' CHECK (status IN ('UNKNOWN', 'ONLINE', 'OFFLINE', 'MAINTENANCE')),
    last_ping_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (tenant_id, site_id, device_code)
);
CREATE INDEX idx_devices_site ON devices(site_id, status);

-- 2. BẢNG SỰ KIỆN NHẬN DIỆN BIỂN SỐ (LPR RECOGNITION EVENTS & EVIDENCE)
-- Ràng buộc SET NULL: Xóa thiết bị camera KHÔNG làm mất dữ liệu chứng cứ LPR.
-- Hỗ trợ upload ảnh thủ công (device_id NULL) và kết quả nhận diện không chắc chắn.
CREATE TABLE lpr_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NULL REFERENCES devices(id) ON DELETE SET NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    
    -- Dữ liệu nhận diện biển số & Trạng thái không chắc chắn
    original_plate VARCHAR(50) NULL,
    normalized_plate VARCHAR(50) NULL,
    confidence_score DECIMAL(5,2) CHECK (confidence_score >= 0 AND confidence_score <= 100),
    recognition_outcome VARCHAR(50) NOT NULL DEFAULT 'SUCCESS' 
        CHECK (recognition_outcome IN ('SUCCESS', 'UNREADABLE', 'MULTIPLE_CANDIDATES', 'TIMEOUT', 'NO_PLATE', 'MANUAL_REVIEW')),
    model_version VARCHAR(50),
    
    -- Chứng cứ Media theo contract v1.0
    image_url VARCHAR(500),
    media_asset_id UUID NULL,
    media_retention_class VARCHAR(50) NOT NULL DEFAULT 'STANDARD_30_DAYS',
    
    -- Hiệu chỉnh thủ công của Operator
    corrected_plate VARCHAR(50) NULL,
    corrected_by UUID NULL,
    corrected_at TIMESTAMPTZ NULL,
    
    direction VARCHAR(50) NOT NULL CHECK (direction IN ('INBOUND', 'OUTBOUND')),
    processed_status VARCHAR(50) NOT NULL DEFAULT 'UNPROCESSED' CHECK (processed_status IN ('UNPROCESSED', 'SYNCED_TO_OPERATIONS')),
    captured_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_lpr_events_site_time ON lpr_events(site_id, captured_at DESC);
CREATE INDEX idx_lpr_events_plate ON lpr_events(normalized_plate);
CREATE INDEX idx_lpr_events_outcome ON lpr_events(recognition_outcome);

-- 3. BẢNG TELEMETRY CẢM BIẾN CHỖ ĐỖ (SENSOR TELEMETRIES)
CREATE TABLE sensor_telemetries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NULL REFERENCES devices(id) ON DELETE SET NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL, -- Logical ID trỏ sang FacilityDb
    is_occupied BOOLEAN NOT NULL,
    detected_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_sensor_telemetries_site_slot ON sensor_telemetries(site_id, slot_id, detected_at DESC);

-- 4. BẢNG HỘI THOẠI TRỢ LÝ ẢO AI (ASSISTANT CONVERSATIONS)
-- Module AI Assistant thuộc quyền quản lý của IoT & Recognition Service theo kiến trúc v1.0
CREATE TABLE assistant_conversations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL, -- Logical ID trỏ sang UserDb
    session_token VARCHAR(255),
    title VARCHAR(255) NOT NULL DEFAULT 'New Conversation',
    context_scope JSONB, -- Lưu trữ ngữ cảnh bãi xe, xe hiện tại đang hỏi
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_assistant_conversations_user ON assistant_conversations(user_id, updated_at DESC);

-- 5. BẢNG TIN NHẮN TRỢ LÝ ẢO AI (ASSISTANT MESSAGES)
CREATE TABLE assistant_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    conversation_id UUID NOT NULL REFERENCES assistant_conversations(id) ON DELETE CASCADE,
    sender_role VARCHAR(50) NOT NULL CHECK (sender_role IN ('USER', 'ASSISTANT', 'SYSTEM', 'TOOL')),
    content TEXT NOT NULL,
    tool_call_id VARCHAR(100) NULL,
    tokens_count INT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_assistant_messages_conversation ON assistant_messages(conversation_id, created_at);

-- 6. BẢNG NHẬT KÝ THỰC THI TOOL CALL CỦA AI (ASSISTANT TOOL RUNS)
CREATE TABLE assistant_tool_runs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    conversation_id UUID NOT NULL REFERENCES assistant_conversations(id) ON DELETE CASCADE,
    message_id UUID NULL REFERENCES assistant_messages(id) ON DELETE SET NULL,
    tool_name VARCHAR(100) NOT NULL,
    input_parameters JSONB NOT NULL,
    output_result JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED')),
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_assistant_tool_runs_conv ON assistant_tool_runs(conversation_id, executed_at);
