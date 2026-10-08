-- ======================================================================================
-- MICROSERVICE: PARKING SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG DOANH NGHIỆP BÃI XE
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    contact_email VARCHAR(255) NOT NULL,
    contact_phone VARCHAR(20) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_tenant_code ON tenants (code) WHERE deleted_at IS NULL;

-- 2. BẢNG BÃI ĐỖ XE
CREATE TABLE parking_sites (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    total_physical_capacity INT NOT NULL DEFAULT 0 CHECK (total_physical_capacity >= 0),
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE', 'SUSPENDED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (id, tenant_id),
    UNIQUE (tenant_id, site_code)
);

-- 3. BẢNG ĐƠN VỊ KHÔNG GIAN PHÂN CẤP
CREATE TABLE spatial_units (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    parent_id UUID,
    path TEXT NOT NULL,
    unit_type VARCHAR(50) NOT NULL CHECK (unit_type IN ('ZONE', 'FLOOR', 'BLOCK')),
    name VARCHAR(100) NOT NULL,
    max_capacity INT NOT NULL DEFAULT 0 CHECK (max_capacity >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (id, tenant_id, site_id),
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE,
    FOREIGN KEY (parent_id, tenant_id, site_id) REFERENCES spatial_units(id, tenant_id, site_id) ON DELETE CASCADE
);

-- 4. BẢNG CHỖ ĐỖ VẬT LÝ
CREATE TABLE parking_slots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    spatial_unit_id UUID NOT NULL,
    slot_code VARCHAR(50) NOT NULL,
    slot_type VARCHAR(50) NOT NULL DEFAULT 'STANDARD' CHECK (slot_type IN ('STANDARD', 'EV', 'DISABLED', 'VIP')),
    features JSONB,
    physical_state VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (physical_state IN ('AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE')), -- 'AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE'
    reservation_state VARCHAR(50) NULL CHECK (reservation_state IN ('RESERVED', 'PROTECTED', 'BACKUP')), -- 'RESERVED', 'PROTECTED', 'BACKUP'
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (id, tenant_id, site_id),
    UNIQUE (tenant_id, site_id, slot_code),
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE,
    FOREIGN KEY (spatial_unit_id, tenant_id, site_id) REFERENCES spatial_units(id, tenant_id, site_id) ON DELETE CASCADE
);

-- 4B. BẢNG MAPPING LOẠI XE CHO CHỖ ĐỖ (SLOT COMPATIBILITIES)
CREATE TABLE slot_compatibilities (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (tenant_id, site_id, slot_id, vehicle_type),
    FOREIGN KEY (slot_id, tenant_id, site_id) REFERENCES parking_slots(id, tenant_id, site_id) ON DELETE CASCADE
);

-- 5. BẢNG VẬT PHẨM ĐỊNH DANH RA VÀO (Thẻ, QR)
CREATE TABLE access_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    item_type VARCHAR(50) NOT NULL CHECK (item_type IN ('CARD', 'QR', 'LICENSE_PLATE', 'RFID')),
    identifier_code VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (status IN ('AVAILABLE', 'IN_USE', 'LOST', 'DISABLED')),
    -- Cross-domain Reference (Logical ID trỏ sang Reservation Service)
    current_session_id UUID NULL, 
    issued_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, identifier_code),
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE
);

-- 6. CẤU HÌNH VẬN HÀNH & BIỂU GIÁ
CREATE TABLE site_operational_settings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    site_id UUID,
    -- Các tham số cấu hình bám sát chuẩn SRS (Mục 3.4.9, 3.4.10, 3.5.4)
    free_parking_grace_minutes INT NOT NULL DEFAULT 15,
    arrival_late_tolerance_minutes INT NOT NULL DEFAULT 15,
    payment_hold_duration_minutes INT NOT NULL DEFAULT 5, -- SRS Default: 5 phút
    min_reservation_duration_minutes INT NOT NULL DEFAULT 30,
    max_reservation_duration_hours INT NOT NULL DEFAULT 24,
    reservation_protection_window_hours INT NOT NULL DEFAULT 4,
    allocation_lead_time_minutes INT NOT NULL DEFAULT 30,
    occupancy_validity_timespan_hours INT NOT NULL DEFAULT 4, -- SRS Default: 4 giờ
    version INT NOT NULL DEFAULT 1,
    -- Cross-domain Reference
    created_by UUID NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE
);

CREATE TABLE tariffs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    version INT NOT NULL DEFAULT 1,
    tariff_rules JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE
);

-- 8. INDEXES FOR PERFORMANCE
CREATE INDEX idx_parking_sites_tenant_id ON parking_sites(tenant_id);
CREATE INDEX idx_spatial_units_tenant_id ON spatial_units(tenant_id);
CREATE INDEX idx_spatial_units_site_id ON spatial_units(site_id);
CREATE INDEX idx_spatial_units_parent_id ON spatial_units(parent_id);
CREATE INDEX idx_parking_slots_tenant_id ON parking_slots(tenant_id);
CREATE INDEX idx_parking_slots_site_id ON parking_slots(site_id);
CREATE INDEX idx_parking_slots_spatial_unit_id ON parking_slots(spatial_unit_id);
CREATE INDEX idx_access_items_site_id ON access_items(site_id);
CREATE INDEX idx_access_items_identifier_code ON access_items(identifier_code);
CREATE INDEX idx_site_settings_site_id ON site_operational_settings(site_id);
CREATE INDEX idx_tariffs_site_id ON tariffs(site_id);

-- 9. BẢNG LUỒNG ĐI (PARKING ACCESS PATHS)
CREATE TABLE parking_access_paths (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    path_code VARCHAR(50) NOT NULL,
    from_unit_id UUID,
    to_unit_id UUID,
    map_data JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ,
    UNIQUE(id, tenant_id, site_id),
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE RESTRICT,
    FOREIGN KEY (from_unit_id, tenant_id, site_id) REFERENCES spatial_units(id, tenant_id, site_id) ON DELETE RESTRICT,
    FOREIGN KEY (to_unit_id, tenant_id, site_id) REFERENCES spatial_units(id, tenant_id, site_id) ON DELETE RESTRICT,
    CHECK (from_unit_id IS NULL OR to_unit_id IS NULL OR from_unit_id <> to_unit_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_access_path_code ON parking_access_paths(site_id, upper(path_code)) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_access_paths_site ON parking_access_paths(site_id);

-- THÊM CÁC INDEX MỚI TỪ NHÁNH DEVELOP
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_site_code_ci ON parking_sites(tenant_id, upper(site_code)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_slot_code_ci ON parking_slots(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_unit_name_ci ON spatial_units(site_id, parent_id, lower(name)) NULLS NOT DISTINCT WHERE deleted_at IS NULL;

-- ======================================================================================
-- 10. BẢNG SỰ CỐ VẬN HÀNH (OPERATIONAL INCIDENTS)
-- [Merged từ 08-incident-service-db.sql]
-- Bounded Context: Sự cố gắn liền với trạng thái vật lý của bãi (slot, thiết bị).
-- Incident workflow (OPEN→IN_PROGRESS→RESOLVED) là trách nhiệm của operator bãi đỗ.
-- Cross-service links dùng Logical ID, không dùng FK vật lý.
-- ======================================================================================
CREATE TABLE incidents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,

    incident_type VARCHAR(50) NOT NULL CHECK (incident_type IN ('LOST_TICKET', 'UNKNOWN_SLOT_STATE', 'CHECKOUT_MISMATCH', 'OTHER')),
    reference_id UUID,   -- Logical ID của slot, session, hoặc thiết bị liên quan

    description TEXT,
    evidence_url VARCHAR(500),

    reported_by UUID,    -- Logical ID: user hoặc system actor (từ User Service)
    assigned_to UUID,    -- Logical ID: operator / owner (từ User Service)

    status VARCHAR(50) NOT NULL DEFAULT 'OPEN' CHECK (status IN ('OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED')),
    resolved_at TIMESTAMPTZ,
    resolution_notes TEXT,

    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    CONSTRAINT chk_incident_resolved_state CHECK (
        (status IN ('OPEN', 'IN_PROGRESS') AND resolved_at IS NULL) OR
        (status IN ('RESOLVED', 'CLOSED') AND resolved_at IS NOT NULL)
    )
);

CREATE INDEX idx_incidents_tenant_site ON incidents(tenant_id, site_id);
CREATE INDEX idx_incidents_status ON incidents(status);
