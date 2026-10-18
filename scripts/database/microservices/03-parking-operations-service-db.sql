-- ======================================================================================
-- MICROSERVICE: PARKING OPERATIONS SERVICE DATABASE (OperationsDb)
-- Domain: Reservations, Parking Sessions, Allocations, Real-time Occupancy Claims,
--         Monthly Passes, Access Items, Incidents, Operational Policies, Search Module
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- 1. BẢNG CẤU HÌNH ĐỊNH MỨC SỨC CHỨA VẬN HÀNH (OPERATIONAL CAPACITY CONFIGS)
-- Lưu trữ cấu hình hạn mức nhận đặt chỗ và sức chứa tối đa phục vụ tính toán khả dụng.
CREATE TABLE operational_capacity_configs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    spatial_unit_id UUID NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    total_physical_capacity INT NOT NULL CHECK (total_physical_capacity >= 0),
    max_reservation_quota INT NOT NULL CHECK (max_reservation_quota >= 0 AND max_reservation_quota <= total_physical_capacity),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (site_id, spatial_unit_id, vehicle_type)
);
CREATE INDEX idx_op_capacity_site ON operational_capacity_configs(site_id, vehicle_type);

-- 2. BẢNG CHÍNH SÁCH VẬN HÀNH (OPERATIONAL POLICIES)
-- Quản lý các tham số cửa sổ thời gian (bounds) theo chuẩn SRS v1.0
CREATE TABLE operational_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NULL, -- NULL nghĩa là default áp dụng toàn hệ thống
    arrival_late_tolerance_minutes INT NOT NULL DEFAULT 15,
    payment_hold_duration_minutes INT NOT NULL DEFAULT 15,
    min_reservation_duration_minutes INT NOT NULL DEFAULT 15,
    max_reservation_duration_hours INT NOT NULL DEFAULT 24,
    reservation_protection_window_hours INT NOT NULL DEFAULT 4,
    allocation_lead_time_minutes INT NOT NULL DEFAULT 60,
    occupancy_validity_timespan_hours INT NOT NULL DEFAULT 4,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (tenant_id, site_id)
);
CREATE INDEX idx_operational_policies_site ON operational_policies(site_id);

-- 3. BẢNG HẠN NGẠCH DỰ PHÒNG (STRUCTURE BACKUP POLICIES)
-- Tiếp nhận từ FacilityDb: Quản lý số lượng slot/capacity giữ làm backup dự phòng
CREATE TABLE structure_backup_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    spatial_unit_id UUID NULL,
    vehicle_type VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    backup_count INT NOT NULL CHECK (backup_count >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX uq_backup_site_policy ON structure_backup_policies(site_id, vehicle_type) WHERE spatial_unit_id IS NULL;
CREATE UNIQUE INDEX uq_backup_unit_policy ON structure_backup_policies(site_id, spatial_unit_id, vehicle_type) WHERE spatial_unit_id IS NOT NULL;

-- 4. BẢNG ĐẶT CHỖ TRƯỚC (RESERVATIONS)
-- Áp dụng cơ chế Time-range overlap; không dùng scalar counter phẳng
CREATE TABLE reservations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID NOT NULL, 
    target_spatial_unit_id UUID,
    target_slot_id UUID,
    
    original_plate VARCHAR(50),
    normalized_plate VARCHAR(50),
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    
    expected_start_time TIMESTAMPTZ NOT NULL,
    expected_end_time TIMESTAMPTZ NOT NULL,
    confirmed_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT' 
        CHECK (status IN ('PENDING_PAYMENT', 'CONFIRMED', 'ALLOCATED', 'PARKING', 'COMPLETED', 'EXPIRED', 'NO_SHOW', 'CANCELLED', 'UNFULFILLABLE')),
    hold_expires_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    accepted_pricing_snapshot JSONB,
    version INT NOT NULL DEFAULT 1, -- Optimistic Concurrency Control
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (expected_end_time > expected_start_time),
    CHECK (NOT (target_slot_id IS NOT NULL AND target_spatial_unit_id IS NOT NULL)),
    CONSTRAINT no_overlapping_reservations_per_vehicle EXCLUDE USING gist (
        tenant_id WITH =,
        site_id WITH =,
        normalized_plate WITH =,
        tstzrange(expected_start_time, expected_end_time) WITH &&
    ) WHERE (status IN ('CONFIRMED', 'ALLOCATED', 'PARKING'))
);
CREATE INDEX idx_reservations_tenant_site ON reservations(tenant_id, site_id);
CREATE INDEX idx_reservations_account ON reservations(account_id);
CREATE INDEX idx_reservations_status_times ON reservations(status, expected_start_time, expected_end_time);
CREATE INDEX idx_reservations_plate ON reservations(normalized_plate);

-- 5. BẢNG PHIÊN ĐỖ XE THỰC TẾ (PARKING SESSIONS)
CREATE TABLE parking_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID REFERENCES reservations(id) ON DELETE SET NULL,
    
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    access_item_id UUID,
    account_id UUID,
    current_slot_id UUID,
    
    original_plate VARCHAR(50),
    normalized_plate VARCHAR(50),
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    ticket_reference VARCHAR(100),
    
    entry_time TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    entry_image_url TEXT,
    
    exit_time TIMESTAMPTZ,
    exit_image_url TEXT,
    
    session_status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' 
        CHECK (session_status IN ('ACTIVE', 'PENDING_PAYMENT', 'COMPLETED', 'OVERSTAYED', 'DISPUTED')),
    version INT NOT NULL DEFAULT 1, -- Optimistic Concurrency Control
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (exit_time IS NULL OR exit_time > entry_time)
);
CREATE INDEX idx_parking_sessions_tenant_site ON parking_sessions(tenant_id, site_id);
CREATE INDEX idx_parking_sessions_account ON parking_sessions(account_id);
CREATE INDEX idx_parking_sessions_status ON parking_sessions(session_status);
CREATE INDEX idx_parking_sessions_plate ON parking_sessions(normalized_plate);
CREATE UNIQUE INDEX idx_parking_sessions_active_ticket ON parking_sessions(ticket_reference) WHERE session_status = 'ACTIVE' AND ticket_reference IS NOT NULL;
CREATE UNIQUE INDEX idx_parking_sessions_active_access ON parking_sessions(access_item_id) WHERE session_status = 'ACTIVE' AND access_item_id IS NOT NULL;

-- 6. BẢNG GÁN TÀI NGUYÊN Ô ĐỖ (SLOT ALLOCATIONS)
CREATE TABLE slot_allocations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID REFERENCES reservations(id) ON DELETE SET NULL,
    parking_session_id UUID REFERENCES parking_sessions(id) ON DELETE SET NULL,
    
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL,
    
    allocated_start_time TIMESTAMPTZ NOT NULL,
    allocated_end_time TIMESTAMPTZ NOT NULL,
    allocation_status VARCHAR(50) NOT NULL DEFAULT 'RESERVED' CHECK (allocation_status IN ('RESERVED', 'OCCUPIED', 'RELEASED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (allocated_end_time > allocated_start_time),
    CONSTRAINT no_overlapping_slot_allocations EXCLUDE USING gist (
        slot_id WITH =,
        tstzrange(allocated_start_time, allocated_end_time) WITH &&
    ) WHERE (allocation_status IN ('RESERVED', 'OCCUPIED'))
);
CREATE INDEX idx_slot_allocations_tenant_site ON slot_allocations(tenant_id, site_id);
CREATE INDEX idx_slot_allocations_times ON slot_allocations(allocated_start_time, allocated_end_time);

-- 7. BẢNG QUẢN LÝ VẬT PHẨM ĐỊNH DANH (ACCESS ITEMS - THẺ TỪ / QR CODE)
-- Tiếp nhận từ FacilityDb: Quản lý vòng đời thẻ từ và phiên sử dụng
CREATE TABLE access_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    item_type VARCHAR(50) NOT NULL CHECK (item_type IN ('CARD', 'QR', 'LICENSE_PLATE', 'RFID')),
    identifier_code VARCHAR(100) NOT NULL,
    card_number VARCHAR(50), -- Số thẻ in trên mặt thẻ vật lý
    status VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (status IN ('AVAILABLE', 'IN_USE', 'LOST', 'DISABLED')),
    current_session_id UUID REFERENCES parking_sessions(id) ON DELETE SET NULL,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMPTZ NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, identifier_code)
);
CREATE INDEX idx_access_items_site_id ON access_items(site_id);
CREATE INDEX idx_access_items_identifier ON access_items(identifier_code);
CREATE INDEX idx_access_items_status ON access_items(status);

-- 8. BẢNG VÉ THÁNG (MONTHLY PASSES - WHEN_SPACE_AVAILABLE ONLY)
-- Chuẩn hóa v1.0: Loại bỏ hoàn toàn GUARANTEED_CAPACITY_SLOT
CREATE TABLE monthly_passes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID NOT NULL,
    
    pass_code VARCHAR(100) NOT NULL UNIQUE,
    entitlement_type VARCHAR(50) NOT NULL DEFAULT 'WHEN_SPACE_AVAILABLE' 
        CHECK (entitlement_type = 'WHEN_SPACE_AVAILABLE'),
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    normalized_plate VARCHAR(50),
    
    valid_from TIMESTAMPTZ NOT NULL,
    valid_until TIMESTAMPTZ NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' 
        CHECK (status IN ('PENDING_PAYMENT', 'ACTIVE', 'EXPIRED', 'CANCELLED')),
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (valid_until > valid_from),
    CONSTRAINT no_overlapping_passes_per_vehicle EXCLUDE USING gist (
        tenant_id WITH =,
        site_id WITH =,
        normalized_plate WITH =,
        tstzrange(valid_from, valid_until) WITH &&
    ) WHERE (status = 'ACTIVE')
);
CREATE INDEX idx_monthly_passes_tenant_site ON monthly_passes(tenant_id, site_id);
CREATE INDEX idx_monthly_passes_account ON monthly_passes(account_id);

-- 9. BẢNG SỰ CỐ VẬN HÀNH (OPERATIONAL INCIDENTS)
-- Tiếp nhận từ FacilityDb: Xử lý sự cố vé, slot, ra vào cổng
CREATE TABLE operational_incidents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    incident_type VARCHAR(50) NOT NULL CHECK (incident_type IN ('LOST_TICKET', 'UNKNOWN_SLOT_STATE', 'CHECKOUT_MISMATCH', 'OTHER')),
    reference_id UUID,
    description TEXT,
    evidence_url VARCHAR(500),
    reported_by UUID,
    assigned_to UUID,
    status VARCHAR(50) NOT NULL DEFAULT 'OPEN' CHECK (status IN ('OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED')),
    resolved_at TIMESTAMPTZ,
    resolution_notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_incident_resolved_state CHECK (
        (status IN ('OPEN', 'IN_PROGRESS') AND resolved_at IS NULL) OR
        (status IN ('RESOLVED', 'CLOSED') AND resolved_at IS NOT NULL)
    )
);
CREATE INDEX idx_operational_incidents_site ON operational_incidents(site_id, status);

CREATE TABLE operator_incident_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    actor_id UUID NOT NULL,
    incident_type VARCHAR(80) NOT NULL,
    details TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'reported' CHECK (status IN ('reported', 'resolved')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_operator_incident_events_site ON operator_incident_events(site_id, created_at DESC);

-- 10. BẢNG CHIẾU TÌM KIẾM BÃI XE (SEARCH & DISCOVERY PROJECTIONS)
-- Module Search & Discovery nằm trong Parking Operations Service theo kiến trúc v1.0
CREATE TABLE search_facility_projections (
    site_id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    site_code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    supported_vehicle_types TEXT[] NOT NULL,
    is_open BOOLEAN NOT NULL DEFAULT TRUE,
    operational_status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
    cached_available_spaces INT NOT NULL DEFAULT 0,
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_search_facility_location ON search_facility_projections(latitude, longitude);
CREATE INDEX idx_search_facility_status ON search_facility_projections(operational_status, is_open);

-- 11. BẢNG NHẬT KÝ KIỂM TOÁN CỔNG RA VÀO (GATE AUDIT LOGS - ZERO CASCADE)
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    actor_id UUID NOT NULL,
    actor_role VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    target_entity VARCHAR(100) NOT NULL,
    target_id UUID NOT NULL,
    reason TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_audit_logs_site_created ON audit_logs(site_id, created_at DESC);
CREATE INDEX idx_audit_logs_actor ON audit_logs(actor_id);
