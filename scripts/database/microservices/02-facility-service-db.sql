-- ======================================================================================
-- MICROSERVICE: FACILITY & VISUALIZATION SERVICE DATABASE (FacilityDb)
-- Domain: Physical Infrastructure, Sites, Floors, Zones, Slots, 3D Layout, Static Capacity
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG BÃI ĐỖ XE VẬT LÝ (PARKING SITES)
CREATE TABLE parking_sites (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Cross-domain Logical Reference to User & Access Service (UserDb)
    tenant_id UUID NOT NULL,
    site_code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    total_physical_capacity INT NOT NULL DEFAULT 0 CHECK (total_physical_capacity >= 0),
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('PENDING_APPROVAL', 'ACTIVE', 'INACTIVE', 'MAINTENANCE', 'SUSPENDED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, site_code)
);
CREATE UNIQUE INDEX uq_active_site_code_ci ON parking_sites(tenant_id, upper(site_code)) WHERE deleted_at IS NULL;
CREATE INDEX idx_parking_sites_tenant_id ON parking_sites(tenant_id);
CREATE INDEX idx_parking_sites_status ON parking_sites(status);

-- 2. BẢNG ĐƠN VỊ KHÔNG GIAN PHÂN CẤP (SPATIAL UNITS - ZONE, FLOOR, BLOCK)
CREATE TABLE spatial_units (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    parent_id UUID REFERENCES spatial_units(id) ON DELETE CASCADE,
    path TEXT NOT NULL,
    unit_type VARCHAR(50) NOT NULL CHECK (unit_type IN ('ZONE', 'FLOOR', 'BLOCK')),
    name VARCHAR(100) NOT NULL,
    max_capacity INT NOT NULL DEFAULT 0 CHECK (max_capacity >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_unit_name_ci ON spatial_units(site_id, parent_id, lower(name)) NULLS NOT DISTINCT WHERE deleted_at IS NULL;
CREATE INDEX idx_spatial_units_tenant_id ON spatial_units(tenant_id);
CREATE INDEX idx_spatial_units_site_id ON spatial_units(site_id);
CREATE INDEX idx_spatial_units_parent_id ON spatial_units(parent_id);

-- 3. BẢNG SỨC CHỨA CẤU HÌNH TĨNH THEO LOẠI XE (SPATIAL UNIT CAPACITIES)
-- Facility Service chỉ sở hữu sức chứa thiết kế tĩnh (max_capacity).
-- Dữ liệu động (current_occupied, reserved_count) thuộc về Parking Operations Service.
CREATE TABLE spatial_unit_capacities (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    spatial_unit_id UUID NOT NULL REFERENCES spatial_units(id) ON DELETE CASCADE,
    vehicle_type VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    max_capacity INT NOT NULL CHECK (max_capacity >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (spatial_unit_id, vehicle_type)
);
CREATE INDEX idx_spatial_unit_capacities_site ON spatial_unit_capacities (site_id, spatial_unit_id, vehicle_type);
CREATE INDEX idx_spatial_unit_capacities_unit ON spatial_unit_capacities (spatial_unit_id);

-- 4. BẢNG CHỖ ĐỖ VẬT LÝ (PARKING SLOTS)
-- Thuần túy lưu trữ tài nguyên vật lý, loại ô, tọa độ 3D và trạng thái vật lý.
-- Trạng thái đặt chỗ (RESERVED, PROTECTED, BACKUP) được quản lý bởi Operations Service.
CREATE TABLE parking_slots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    spatial_unit_id UUID NOT NULL REFERENCES spatial_units(id) ON DELETE CASCADE,
    slot_code VARCHAR(50) NOT NULL,
    slot_type VARCHAR(50) NOT NULL DEFAULT 'STANDARD' CHECK (slot_type IN ('STANDARD', 'EV', 'DISABLED', 'VIP')),
    features JSONB,
    coordinates_3d JSONB NULL,
    physical_state VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (physical_state IN ('AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, site_id, slot_code),
    CONSTRAINT ck_deleted_slot_not_occupied CHECK (deleted_at IS NULL OR (physical_state NOT IN ('OCCUPIED', 'UNKNOWN')))
);
CREATE UNIQUE INDEX uq_active_slot_code_ci ON parking_slots(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL;
CREATE INDEX idx_parking_slots_tenant_id ON parking_slots(tenant_id);
CREATE INDEX idx_parking_slots_site_id ON parking_slots(site_id);
CREATE INDEX idx_parking_slots_spatial_unit_id ON parking_slots(spatial_unit_id);
CREATE INDEX idx_parking_slots_physical_state ON parking_slots(physical_state);

-- 5. BẢNG TƯƠNG THÍCH LOẠI XE CHO CHỖ ĐỖ (SLOT COMPATIBILITIES)
CREATE TABLE slot_compatibilities (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL REFERENCES parking_slots(id) ON DELETE CASCADE,
    vehicle_type VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (slot_id, vehicle_type)
);
CREATE INDEX idx_slot_compatibilities_slot ON slot_compatibilities(slot_id);
CREATE INDEX idx_slot_compatibilities_type ON slot_compatibilities(vehicle_type);

-- 6. BẢNG LUỒNG ĐI NỘI BỘ (PARKING ACCESS PATHS)
CREATE TABLE parking_access_paths (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    path_code VARCHAR(50) NOT NULL,
    from_unit_id UUID REFERENCES spatial_units(id) ON DELETE RESTRICT,
    to_unit_id UUID REFERENCES spatial_units(id) ON DELETE RESTRICT,
    map_data JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ,
    CHECK (from_unit_id IS NULL OR to_unit_id IS NULL OR from_unit_id <> to_unit_id)
);
CREATE UNIQUE INDEX uq_active_access_path_code ON parking_access_paths(site_id, upper(path_code)) WHERE deleted_at IS NULL;
CREATE INDEX idx_access_paths_site ON parking_access_paths(site_id);

-- 7. CÁC THAO TÁC CẤU TRÚC (STRUCTURE OPERATIONS)
CREATE TABLE structure_operations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    tenant_id UUID NOT NULL,
    actor_id UUID NOT NULL,
    idempotency_key UUID NOT NULL,
    request JSONB NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('pending','failed','completed')),
    physical_committed BOOLEAN NOT NULL DEFAULT FALSE,
    impact JSONB,
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(site_id, idempotency_key)
);
CREATE INDEX idx_structure_operations_site ON structure_operations(site_id);

-- 8. BẢNG KIỂM TOÁN CHỖ ĐỖ (STRUCTURE SLOT AUDIT - ZERO CASCADE)
-- Ràng buộc RESTRICT/SET NULL: Không cho phép CASCADE xóa mất dữ liệu kiểm toán.
CREATE TABLE structure_slot_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    slot_id UUID REFERENCES parking_slots(id) ON DELETE SET NULL,
    actor_id UUID NOT NULL,
    action TEXT NOT NULL,
    reason TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_structure_slot_audit_site ON structure_slot_audit(site_id);
CREATE INDEX idx_structure_slot_audit_slot ON structure_slot_audit(slot_id);

-- 9. BẢNG KIỂM TOÁN THAY ĐỔI CẤU TRÚC (STRUCTURE CHANGE AUDIT - ZERO CASCADE)
CREATE TABLE structure_change_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    actor_id UUID NOT NULL,
    operation_id UUID REFERENCES structure_operations(id) ON DELETE SET NULL,
    before_state JSONB,
    after_state JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX ix_structure_change_history ON structure_change_audit(site_id, created_at);
