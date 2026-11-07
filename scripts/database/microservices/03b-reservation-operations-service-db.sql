-- ======================================================================================
-- MICROSERVICE: PARKING OPERATIONS SERVICE DATABASE (OperationsDb) - PART 03b
-- Domain: Reservations, Sessions, Allocations, Access, Passes, Incidents, Search, Audit
-- Mounted into smartpark_operations DB
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- 1. BẢNG CẤU HÌNH ĐỊNH MỨC SỨC CHỨA VẬN HÀNH (OPERATIONAL CAPACITY CONFIGS)
-- Lưu trữ cấu hình hạn mức nhận đặt chỗ và sức chứa tối đa phục vụ tính toán khả dụng.
CREATE TABLE IF NOT EXISTS operational_capacity_configs (
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
CREATE INDEX IF NOT EXISTS idx_op_capacity_site ON operational_capacity_configs(site_id, vehicle_type);

-- 2. BẢNG CHÍNH SÁCH VẬN HÀNH (OPERATIONAL POLICIES)
-- Quản lý các tham số cửa sổ thời gian (bounds) theo chuẩn SRS v1.0
CREATE TABLE IF NOT EXISTS operational_policies (
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
CREATE INDEX IF NOT EXISTS idx_operational_policies_site ON operational_policies(site_id);

-- 3. BẢNG POOL SỨC CHỨA THEO BÃI/KHU VỰC (SITE CAPACITY POOLS)
-- Quản lý quota và số lượng xe động phục vụ chống overbooking
CREATE TABLE IF NOT EXISTS site_capacity_pools (
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
CREATE INDEX IF NOT EXISTS idx_site_capacity_pools_tenant_site ON site_capacity_pools(tenant_id, site_id);

-- 4. BẢNG ĐẶT CHỖ TRƯỚC (RESERVATIONS)
-- Áp dụng cơ chế Time-range overlap; không dùng scalar counter phẳng
CREATE TABLE IF NOT EXISTS reservations (
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
CREATE INDEX IF NOT EXISTS idx_reservations_tenant_site ON reservations(tenant_id, site_id);
CREATE INDEX IF NOT EXISTS idx_reservations_account ON reservations(account_id);
CREATE INDEX IF NOT EXISTS idx_reservations_status_times ON reservations(status, expected_start_time, expected_end_time);
CREATE INDEX IF NOT EXISTS idx_reservations_plate ON reservations(normalized_plate);

-- 5. BẢNG PHIÊN ĐỖ XE THỰC TẾ (PARKING SESSIONS)
CREATE TABLE IF NOT EXISTS parking_sessions (
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
CREATE INDEX IF NOT EXISTS idx_parking_sessions_tenant_site ON parking_sessions(tenant_id, site_id);
CREATE INDEX IF NOT EXISTS idx_parking_sessions_account ON parking_sessions(account_id);
CREATE INDEX IF NOT EXISTS idx_parking_sessions_status ON parking_sessions(session_status);
CREATE INDEX IF NOT EXISTS idx_parking_sessions_plate ON parking_sessions(normalized_plate);
CREATE UNIQUE INDEX IF NOT EXISTS idx_parking_sessions_active_ticket ON parking_sessions(ticket_reference) WHERE session_status = 'ACTIVE' AND ticket_reference IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_parking_sessions_active_access ON parking_sessions(access_item_id) WHERE session_status = 'ACTIVE' AND access_item_id IS NOT NULL;

-- 6. BẢNG GÁN TÀI NGUYÊN Ô ĐỖ (SLOT ALLOCATIONS)
CREATE TABLE IF NOT EXISTS slot_allocations (
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
CREATE INDEX IF NOT EXISTS idx_slot_allocations_tenant_site ON slot_allocations(tenant_id, site_id);
CREATE INDEX IF NOT EXISTS idx_slot_allocations_times ON slot_allocations(allocated_start_time, allocated_end_time);

-- 7. BẢNG QUẢN LÝ VẬT PHẨM ĐỊNH DANH (ACCESS ITEMS - THẺ TỪ / QR CODE)
CREATE TABLE IF NOT EXISTS access_items (
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
CREATE INDEX IF NOT EXISTS idx_access_items_site_id ON access_items(site_id);
CREATE INDEX IF NOT EXISTS idx_access_items_identifier ON access_items(identifier_code);
CREATE INDEX IF NOT EXISTS idx_access_items_status ON access_items(status);

-- 8. BẢNG VÉ THÁNG (MONTHLY PASSES - WHEN_SPACE_AVAILABLE ONLY)
-- Chuẩn hóa v1.0: Loại bỏ hoàn toàn GUARANTEED_CAPACITY_SLOT
CREATE TABLE IF NOT EXISTS monthly_passes (
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
CREATE INDEX IF NOT EXISTS idx_monthly_passes_tenant_site ON monthly_passes(tenant_id, site_id);
CREATE INDEX IF NOT EXISTS idx_monthly_passes_account ON monthly_passes(account_id);

-- 9. BẢNG SỰ CỐ VẬN HÀNH (OPERATIONAL INCIDENTS)
CREATE TABLE IF NOT EXISTS operational_incidents (
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
CREATE INDEX IF NOT EXISTS idx_operational_incidents_site ON operational_incidents(site_id, status);

-- 10. BẢNG CHIẾU TÌM KIẾM BÃI XE (SEARCH & DISCOVERY PROJECTIONS)
CREATE TABLE IF NOT EXISTS search_facility_projections (
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
CREATE INDEX IF NOT EXISTS idx_search_facility_location ON search_facility_projections(latitude, longitude);
CREATE INDEX IF NOT EXISTS idx_search_facility_status ON search_facility_projections(operational_status, is_open);

-- 11. BẢNG NHẬT KÝ KIỂM TOÁN CỔNG RA VÀO (GATE AUDIT LOGS - ZERO CASCADE)
CREATE TABLE IF NOT EXISTS audit_logs (
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
CREATE INDEX IF NOT EXISTS idx_audit_logs_site_created ON audit_logs(site_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_actor ON audit_logs(actor_id);

-- 12. BẢO VỆ AN TOÀN CẤU TRÚC, KHÓA GIỮ VÀ BIÊN NHẬN (STRUCTURE SAFETY & EDIT HOLDS)
-- Quản lý quá trình sửa đổi cấu trúc 3D với 2PC handshake
CREATE TABLE IF NOT EXISTS structure_edit_holds (
    site_id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    token UUID NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS structure_site_state (
    site_id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    active BOOLEAN NOT NULL
);

CREATE TABLE IF NOT EXISTS structure_release_receipts (
    token UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    outcome JSONB NOT NULL,
    completed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Trigger Function: guard_structure_edit()
-- Ngăn các cam kết mới khi bãi đang trong phiên chỉnh sửa cấu trúc hoặc đã inactive
CREATE OR REPLACE FUNCTION guard_structure_edit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE live BOOLEAN;
BEGIN
    IF TG_OP='UPDATE' AND (NEW.site_id<>OLD.site_id OR NEW.tenant_id<>OLD.tenant_id) THEN
        RAISE EXCEPTION 'Commitment scope cannot change' USING ERRCODE='23514';
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.site_id::text, 0));
    IF EXISTS (SELECT 1 FROM structure_edit_holds WHERE site_id=NEW.site_id) THEN
        RAISE EXCEPTION 'Structure edit is in progress; retry later' USING ERRCODE='55P03';
    END IF;
    IF TG_TABLE_NAME='reservations' THEN
        live := NEW.status IN ('PENDING_PAYMENT','CONFIRMED','ALLOCATED','PARKING');
    ELSIF TG_TABLE_NAME='parking_sessions' THEN
        live := NEW.exit_time IS NULL;
    ELSIF TG_TABLE_NAME='slot_allocations' THEN
        live := NEW.allocation_status IN ('RESERVED','OCCUPIED');
    ELSE
        live := NEW.current_reserved_count>0 OR NEW.pending_payment_count>0
            OR NEW.occupied_count>0 OR NEW.protected_count>0 OR NEW.backup_count>0;
    END IF;
    IF live AND EXISTS (
        SELECT 1 FROM structure_site_state WHERE site_id=NEW.site_id AND (NOT active OR tenant_id<>NEW.tenant_id)
    ) THEN RAISE EXCEPTION 'Resource is inactive' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END $$;

DROP TABLE IF EXISTS structure_removed_resources;
DROP TRIGGER IF EXISTS guard_structure_edit ON reservations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON reservations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON parking_sessions;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON parking_sessions FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON slot_allocations;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON slot_allocations FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();
DROP TRIGGER IF EXISTS guard_structure_edit ON site_capacity_pools;
CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON site_capacity_pools FOR EACH ROW EXECUTE FUNCTION guard_structure_edit();

-- 13. TRIGGER TỰ ĐỘNG GHI NHẬN THỜI ĐIỂM XÁC NHẬN ĐẶT CHỖ (RESERVATION CONFIRMATION TIME)
CREATE OR REPLACE FUNCTION record_reservation_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.status IN ('CONFIRMED','ALLOCATED','PARKING') AND NEW.confirmed_at IS NULL THEN
  IF TG_OP='INSERT' THEN NEW.confirmed_at=clock_timestamp();
  ELSIF OLD.status NOT IN ('CONFIRMED','ALLOCATED','PARKING') THEN NEW.confirmed_at=clock_timestamp();
  END IF;
 END IF;
 IF TG_OP='UPDATE' AND OLD.confirmed_at IS NOT NULL THEN NEW.confirmed_at=OLD.confirmed_at; END IF;
 RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS reservation_confirmation_time ON reservations;
CREATE TRIGGER reservation_confirmation_time BEFORE INSERT OR UPDATE ON reservations
 FOR EACH ROW EXECUTE FUNCTION record_reservation_confirmation();
