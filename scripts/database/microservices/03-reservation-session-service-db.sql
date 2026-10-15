-- ======================================================================================
-- MICROSERVICE: RESERVATION & SESSION SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- 1. BẢNG QUOTA SỨC CHỨA (Quản lý số lượng chỗ dư ra để cho phép đặt trước)
CREATE TABLE site_capacity_pools (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL, 
    spatial_unit_id UUID,
    
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    total_capacity INT NOT NULL,
    max_reservation_quota INT NOT NULL,
    current_reserved_count INT NOT NULL DEFAULT 0,
    pending_payment_count INT NOT NULL DEFAULT 0,
    occupied_count INT NOT NULL DEFAULT 0,
    protected_count INT NOT NULL DEFAULT 0,
    backup_count INT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    CHECK (current_reserved_count >= 0 AND current_reserved_count <= max_reservation_quota),
    CHECK (max_reservation_quota <= total_capacity)
);

CREATE INDEX idx_site_capacity_pools_tenant_site ON site_capacity_pools(tenant_id, site_id);

-- 2. BẢNG ĐẶT CHỖ TRƯỚC
CREATE TABLE reservations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID NOT NULL, 
    target_spatial_unit_id UUID,
    target_slot_id UUID,
    
    -- Data Duplication (Cache) để giảm query chéo
    original_plate VARCHAR(50),
    normalized_plate VARCHAR(50),
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    
    expected_start_time TIMESTAMPTZ NOT NULL,
    expected_end_time TIMESTAMPTZ NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT' CHECK (status IN ('PENDING_PAYMENT', 'CONFIRMED', 'ALLOCATED', 'PARKING', 'COMPLETED', 'EXPIRED', 'NO_SHOW', 'CANCELLED', 'UNFULFILLABLE')),
    hold_expires_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    accepted_pricing_snapshot JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    CHECK (expected_end_time > expected_start_time),
    CHECK (NOT (target_slot_id IS NOT NULL AND target_spatial_unit_id IS NOT NULL))
);

CREATE INDEX idx_reservations_tenant_site ON reservations(tenant_id, site_id);
CREATE INDEX idx_reservations_account ON reservations(account_id);
CREATE INDEX idx_reservations_status_times ON reservations(status, expected_start_time, expected_end_time);
CREATE INDEX idx_reservations_plate ON reservations(normalized_plate);

-- 3. BẢNG PHIÊN ĐỖ XE THỰC TẾ
CREATE TABLE parking_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID,
    
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    access_item_id UUID,
    account_id UUID,
    current_slot_id UUID,
    
    -- Data Duplication
    original_plate VARCHAR(50),
    normalized_plate VARCHAR(50),
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    ticket_reference VARCHAR(100),
    
    entry_time TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    entry_image_url VARCHAR(500),
    
    exit_time TIMESTAMPTZ,
    exit_image_url VARCHAR(500),
    
    session_status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (session_status IN ('ACTIVE', 'PENDING_PAYMENT', 'COMPLETED', 'OVERSTAYED', 'DISPUTED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    CHECK (exit_time IS NULL OR exit_time > entry_time),
    FOREIGN KEY (reservation_id, tenant_id, site_id) REFERENCES reservations(id, tenant_id, site_id) ON DELETE SET NULL
);

CREATE INDEX idx_parking_sessions_tenant_site ON parking_sessions(tenant_id, site_id);
CREATE INDEX idx_parking_sessions_account ON parking_sessions(account_id);
CREATE INDEX idx_parking_sessions_status ON parking_sessions(session_status);
CREATE INDEX idx_parking_sessions_plate ON parking_sessions(normalized_plate);
CREATE INDEX idx_parking_sessions_ticket ON parking_sessions(ticket_reference);

CREATE UNIQUE INDEX idx_parking_sessions_active_ticket ON parking_sessions(ticket_reference) WHERE session_status = 'ACTIVE' AND ticket_reference IS NOT NULL;
CREATE UNIQUE INDEX idx_parking_sessions_active_access ON parking_sessions(access_item_id) WHERE session_status = 'ACTIVE' AND access_item_id IS NOT NULL;

-- 4. BẢNG GÁN TÀI NGUYÊN VẬT LÝ (Chống Double-Booking)
CREATE TABLE slot_allocations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID,
    parking_session_id UUID,
    
    -- Cross-domain References
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
        TSTZRANGE(allocated_start_time, allocated_end_time) WITH &&
    ) WHERE (allocation_status IN ('RESERVED', 'OCCUPIED')),
    FOREIGN KEY (reservation_id, tenant_id, site_id) REFERENCES reservations(id, tenant_id, site_id) ON DELETE SET NULL,
    FOREIGN KEY (parking_session_id, tenant_id, site_id) REFERENCES parking_sessions(id, tenant_id, site_id) ON DELETE SET NULL
);

CREATE INDEX idx_slot_allocations_tenant_site ON slot_allocations(tenant_id, site_id);
CREATE INDEX idx_slot_allocations_times ON slot_allocations(allocated_start_time, allocated_end_time);
