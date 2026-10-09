-- ======================================================================================
-- MICROSERVICE: IOT SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG DANH SÁCH THIẾT BỊ (DEVICES)
CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    
    device_code VARCHAR(100) NOT NULL,
    device_type VARCHAR(50) NOT NULL CHECK (device_type IN ('CAMERA_LPR', 'SENSOR', 'BARRIER_GATE')), -- 'CAMERA_LPR', 'SENSOR', 'BARRIER_GATE'
    location_desc VARCHAR(255),
    ip_address VARCHAR(45),
    mac_address VARCHAR(45),
    status VARCHAR(50) NOT NULL DEFAULT 'UNKNOWN' CHECK (status IN ('UNKNOWN', 'ONLINE', 'OFFLINE', 'MAINTENANCE')), -- 'UNKNOWN', 'ONLINE', 'OFFLINE', 'MAINTENANCE'
    last_ping_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    UNIQUE (tenant_id, site_id, device_code)
);
CREATE INDEX idx_devices_tenant_site ON devices(tenant_id, site_id);

-- 2. BẢNG LỊCH SỬ NHẬN DIỆN BIỂN SỐ (LPR EVENTS)
CREATE TABLE lpr_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    
    -- Dữ liệu OCR Camera trả về
    original_plate VARCHAR(50) NOT NULL,
    normalized_plate VARCHAR(50) NOT NULL,
    confidence_score DECIMAL(5,2) CHECK (confidence_score >= 0 AND confidence_score <= 100), -- vd: 98.50 (%)
    image_url VARCHAR(500), -- Link ảnh chụp biển số
    
    direction VARCHAR(50) NOT NULL CHECK (direction IN ('INBOUND', 'OUTBOUND')), -- 'INBOUND', 'OUTBOUND'
    processed_status VARCHAR(50) NOT NULL DEFAULT 'UNPROCESSED' CHECK (processed_status IN ('UNPROCESSED', 'SYNCED_TO_PARKING')), -- 'UNPROCESSED', 'SYNCED_TO_PARKING'
    captured_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (device_id, tenant_id, site_id) REFERENCES devices(id, tenant_id, site_id) ON DELETE CASCADE
);
CREATE INDEX idx_lpr_events_tenant_site ON lpr_events(tenant_id, site_id);
CREATE INDEX idx_lpr_events_plate_normalized ON lpr_events(normalized_plate);
CREATE INDEX idx_lpr_events_captured_at ON lpr_events(captured_at);

-- 3. BẢNG TELEMETRY (CẢM BIẾN TRẠNG THÁI CHỖ ĐỖ)
CREATE TABLE sensor_telemetries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    
    -- Cross-domain Reference
    slot_id UUID NOT NULL, -- Logical ID trỏ sang Parking Service
    
    is_occupied BOOLEAN NOT NULL,
    detected_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (device_id, tenant_id, site_id) REFERENCES devices(id, tenant_id, site_id) ON DELETE CASCADE
);
CREATE INDEX idx_sensor_telemetries_tenant_site ON sensor_telemetries(tenant_id, site_id);
CREATE INDEX idx_sensor_telemetries_slot_id ON sensor_telemetries(slot_id);
CREATE INDEX idx_sensor_telemetries_detected_at ON sensor_telemetries(detected_at);
