-- ======================================================================================
-- SMARTPARK MANAGEMENT PLATFORM (B2B2C MULTI-TENANT SAAS)
-- POSTGRESQL 16+ PHYSICAL DATABASE IMPLEMENTATION SCRIPT
-- ======================================================================================
-- File: 05.1-Database-Scripts.sql
-- Phiên bản: v2.0 (E-Commerce Payment Architecture & Account-Tier Scalable Model)
-- Target Database: PostgreSQL 16+
-- Kích hoạt Extension yêu cầu: uuid-ossp, btree_gist
-- ======================================================================================

-- 0. KHỞI TẠO EXTENSIONS
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- ======================================================================================
-- MODULE 1: TENANCY & SPATIAL INFRASTRUCTURE (ĐA DOANH NGHIỆP & CÂY KHÔNG GIAN)
-- ======================================================================================

-- 1.1. BẢNG DOANH NGHIỆP BÃI XE (TENANTS)
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'SUSPENDED', 'TERMINATED'
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_tenant_code ON tenants (code) WHERE deleted_at IS NULL;

-- 1.2. BẢNG BÃI ĐỖ XE (PARKING SITES)
CREATE TABLE parking_sites (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    total_physical_capacity INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_site_code ON parking_sites (tenant_id, site_code) WHERE deleted_at IS NULL;
CREATE INDEX idx_parking_sites_geo ON parking_sites (latitude, longitude);

-- 1.3. BẢNG ĐƠN VỊ KHÔNG GIAN PHÂN CẤP (SPATIAL UNITS - MATERIALIZED PATH)
CREATE TABLE spatial_units (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    parent_id UUID REFERENCES spatial_units(id) ON DELETE CASCADE,
    path TEXT NOT NULL, -- vd: '/site_1/floor_b1/zone_a/'
    unit_type VARCHAR(50) NOT NULL, -- 'BUILDING', 'FLOOR', 'ZONE', 'ROW', 'SECTION'
    name VARCHAR(100) NOT NULL,
    max_capacity INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_spatial_units_path ON spatial_units (path text_pattern_ops);
CREATE INDEX idx_spatial_units_site ON spatial_units (site_id);

-- 1.4. BẢNG CHỖ ĐỖ VẬT LÝ (PARKING SLOTS - RÕ LOẠI XE & TIỆN ÍCH)
CREATE TABLE parking_slots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE, -- Bổ sung: Tối ưu hiệu năng truy vấn danh sách slot theo bãi
    spatial_unit_id UUID NOT NULL REFERENCES spatial_units(id) ON DELETE CASCADE,
    slot_code VARCHAR(50) NOT NULL,
    supported_vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN', -- 'MOTORBIKE', 'SEDAN', 'SUV', 'VAN', 'TRUCK'
    slot_type VARCHAR(50) NOT NULL DEFAULT 'STANDARD', -- 'STANDARD', 'VIP', 'EV_CHARGING', 'HANDICAPPED'
    coordinates_3d JSONB, -- (Optional) Dành cho tương lai Indoor Navigation/3D. Nếu chỉ làm 2D có thể để NULL.
    features JSONB, -- { "covered": true, "has_ev_charger": true, "ev_power_kw": 22, "max_dimensions": { "height": 2.2, "length": 5.2 } }
    operational_status VARCHAR(50) NOT NULL DEFAULT 'OPERATIONAL', -- 'OPERATIONAL', 'MAINTENANCE', 'UNKNOWN', 'BLOCKED'
    is_physically_occupied BOOLEAN NOT NULL DEFAULT false, -- Cờ này dành cho tích hợp Cảm biến IoT/Camera. Nếu chỉ quẹt thẻ thì đồng bộ với status phần mềm.
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_slot_code ON parking_slots (spatial_unit_id, slot_code) WHERE deleted_at IS NULL;
CREATE INDEX idx_slots_type_status ON parking_slots (supported_vehicle_type, operational_status);
CREATE INDEX idx_parking_slots_site ON parking_slots (site_id); -- Index phục vụ query nhanh toàn bộ slot của 1 bãi

-- ======================================================================================
-- MODULE 2: IDENTITY, ACCOUNTS, ROLES & SECURITY (IAM SCALABLE & JWT SESSIONS)
-- ======================================================================================

-- 2.1. BẢNG ĐỊNH DANH CON NGƯỜI (USERS - GLOBAL IDENTITY)
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(255),
    password_hash VARCHAR(255) NOT NULL,
    failed_login_attempts INTEGER NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    full_name VARCHAR(255) NOT NULL,
    avatar_url TEXT,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'LOCKED', 'PENDING_VERIFICATION'
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_user_phone ON users (phone) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX uq_active_user_email ON users (email) WHERE deleted_at IS NULL AND email IS NOT NULL;

-- 2.2. BẢNG TÀI KHOẢN NGƯỜI DÙNG PHÂN THEO NGỮ CẢNH (ACCOUNTS - HỖ TRỢ SET TIER & LOYALTY)
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    account_type VARCHAR(50) NOT NULL, -- 'DRIVER', 'BUSINESS_OPERATOR', 'PLATFORM_STAFF'
    tenant_id UUID REFERENCES tenants(id) ON DELETE CASCADE,     -- NULL nếu là Driver / Platform Staff
    site_id UUID REFERENCES parking_sites(id) ON DELETE CASCADE, -- Gắn với bãi cụ thể nếu là Site Operator
    membership_tier VARCHAR(50) NOT NULL DEFAULT 'STANDARD',    -- 'STANDARD', 'SILVER', 'GOLD', 'PLATINUM', 'VIP'
    loyalty_points INT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'SUSPENDED'
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_accounts_user ON accounts (user_id);
CREATE INDEX idx_accounts_tier ON accounts (membership_tier);

-- 2.3. BẢNG DANH MỤC VAI TRÒ (ROLES)
CREATE TABLE roles (
    code VARCHAR(50) PRIMARY KEY, -- 'PLATFORM_ADMIN', 'BUSINESS_OWNER', 'SITE_OPERATOR', 'DRIVER'
    name VARCHAR(100) NOT NULL,
    description TEXT
);

INSERT INTO roles (code, name, description) VALUES
('PLATFORM_ADMIN', 'Platform Administrator', 'Quản trị viên toàn hệ thống SmartPark'),
('BUSINESS_OWNER', 'Parking Business Owner', 'Chủ doanh nghiệp bãi xe, quản trị trong phạm vi tenant'),
('SITE_OPERATOR', 'Site Operator / Attendant', 'Nhân viên bảo vệ / thu ngân trực tại bãi đỗ xe'),
('DRIVER', 'Driver / Customer', 'Tài xế / khách hàng cuối sử dụng dịch vụ');

-- 2.4. BẢNG PHÂN VAI TRÒ CHO TÀI KHOẢN (ACCOUNT_ROLES)
CREATE TABLE account_roles (
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT,
    PRIMARY KEY (account_id, role_code)
);

-- 2.5. BẢNG PHƯƠNG TIỆN (VEHICLES)
CREATE TABLE vehicles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID REFERENCES accounts(id) ON DELETE SET NULL, -- Gắn với Driver Account
    plate_number VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN', -- 'MOTORBIKE', 'SEDAN', 'SUV', 'VAN', 'TRUCK'
    brand VARCHAR(100),
    color VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_vehicle_plate ON vehicles (plate_number) WHERE deleted_at IS NULL;
CREATE INDEX idx_vehicles_account ON vehicles (account_id);

-- 2.6. BẢNG QUẢN LÝ REFRESH TOKEN (JWT SESSION MANAGEMENT & REVOCATION)
CREATE TABLE user_refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    access_token_id UUID NOT NULL UNIQUE,
    access_expires_at TIMESTAMPTZ NOT NULL,
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    device_info VARCHAR(255),
    ip_address VARCHAR(45),
    expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_refresh_tokens_lookup ON user_refresh_tokens (user_id, is_revoked);

-- ======================================================================================
-- MODULE 3: ACCESS ITEMS & TOKENS (VẬT PHẨM ĐỊNH DANH RA VÀO)
-- ======================================================================================

-- 3.1. BẢNG VẬT PHẨM ĐỊNH DANH RA VÀO (ACCESS ITEMS)
-- Có trường cache con trỏ current_session_id để quẹt thẻ là ra ngay phiên đỗ đang chạy
CREATE TABLE access_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    item_type VARCHAR(50) NOT NULL, -- 'PAPER_QR_TICKET', 'RFID_CARD', 'PLATE_NUMBER_LPR', 'VIRTUAL_APP_TOKEN'
    identifier_code VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE', -- 'AVAILABLE', 'ASSIGNED_IN_USE', 'LOST'
    current_session_id UUID NULL, -- Con trỏ trỏ tới parking_sessions đang active (SET NULL khi xe ra)
    issued_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_site_identifier ON access_items (site_id, item_type, identifier_code) WHERE deleted_at IS NULL;
CREATE INDEX idx_access_items_lookup ON access_items (site_id, identifier_code);

-- ======================================================================================
-- MODULE 4: STRONGLY-TYPED POLICIES & TARIFFS (CẤU HÌNH NGHIỆP VỤ TÁCH BẠCH)
-- ======================================================================================

-- 4.1. BẢNG CẤU HÌNH THAM SỐ VẬN HÀNH BÃI (SITE OPERATIONAL SETTINGS)
CREATE TABLE site_operational_settings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    site_id UUID REFERENCES parking_sites(id) ON DELETE CASCADE, -- NULL nếu là cấu hình mặc định tenant
    free_parking_grace_minutes INT NOT NULL DEFAULT 15,
    arrival_late_tolerance_minutes INT NOT NULL DEFAULT 15,
    payment_hold_duration_minutes INT NOT NULL DEFAULT 15,
    min_booking_duration_minutes INT NOT NULL DEFAULT 30,
    max_advance_booking_hours INT NOT NULL DEFAULT 24,
    inter_booking_buffer_minutes INT NOT NULL DEFAULT 15,
    max_active_bookings_per_user INT NOT NULL DEFAULT 3,
    version INT NOT NULL DEFAULT 1,
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_to TIMESTAMPTZ NULL,
    created_by UUID REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_site_operational_settings_effective ON site_operational_settings (site_id, effective_from);

-- 4.2. BẢNG BIỂU GIÁ TÍNH PHÍ (TARIFFS - TIME-SLICING & THEO LOẠI XE)
CREATE TABLE tariffs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN', -- 'MOTORBIKE', 'SEDAN', 'SUV', 'VAN', 'TRUCK'
    tariff_rules JSONB NOT NULL, -- Quy tắc block, ngày/đêm cắt lát (Time-slicing Quyết định 10)
    is_active BOOLEAN NOT NULL DEFAULT true,
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_tariffs_lookup ON tariffs (site_id, vehicle_type, is_active);

-- 4.3. BẢNG BẬC THANG HỦY ĐẶT CHỖ & HOÀN TIỀN (CANCELLATION RULES)
CREATE TABLE cancellation_rules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    min_notice_minutes INT NOT NULL, -- vd: 60 (trước 60 phút), 15 (trước 15 phút)
    refund_percentage DECIMAL(5,2) NOT NULL, -- vd: 100.00 (hoàn 100%), 50.00 (hoàn 50%), 0.00 (không hoàn)
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE INDEX idx_cancellation_rules_site ON cancellation_rules (site_id, min_notice_minutes);

-- ======================================================================================
-- MODULE 5: CAPACITY, RESERVATIONS & 3-TIER SPLIT (CÔNG SUẤT & ĐẶT CHỖ)
-- ======================================================================================

-- 5.1. BẢNG QUOTA SỨC CHỨA CÔ LẬP THEO LOẠI XE (SITE CAPACITY POOLS)
CREATE TABLE site_capacity_pools (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    spatial_unit_id UUID REFERENCES spatial_units(id) ON DELETE CASCADE,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN', -- Phân tách rạch ròi xe máy vs ô tô
    total_capacity INT NOT NULL,
    max_reservation_quota INT NOT NULL,
    emergency_backup_quota INT NOT NULL DEFAULT 0,
    current_reserved_count INT NOT NULL DEFAULT 0,
    version INT NOT NULL DEFAULT 1,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_site_unit_vehicle_pool UNIQUE NULLS NOT DISTINCT (site_id, spatial_unit_id, vehicle_type)
);

-- 5.2. BẢNG ĐẶT CHỖ TRƯỚC (RESERVATIONS - TẦNG 1: INTENT)
-- Bỏ lưu cứng deposit_amount; tiền cọc được quản lý qua Payment Order
CREATE TABLE reservations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT, -- Driver Account bắt buộc
    target_spatial_unit_id UUID REFERENCES spatial_units(id),
    target_slot_id UUID REFERENCES parking_slots(id),
    plate_number VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN',
    reserved_period TSTZRANGE NOT NULL,
    deposit_status VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT', -- 'NOT_REQUIRED', 'PENDING_PAYMENT', 'PAID', 'REFUNDED'
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT', -- 'PENDING_PAYMENT', 'CONFIRMED', 'CHECKED_IN', 'CANCELLED', 'NO_SHOW'
    hold_expires_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    cancellation_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_reservations_account ON reservations (account_id);
CREATE INDEX idx_reservations_site_period ON reservations (site_id, reserved_period);

-- 5.3. BẢNG GÁN TÀI NGUYÊN VẬT LÝ (SLOT ALLOCATIONS - TẦNG 2: BINDING)
-- BẢO VỆ CHỐNG DOUBLE-BOOKING BẰNG POSTGRESQL GIST EXCLUSION CONSTRAINT
CREATE TABLE slot_allocations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    slot_id UUID NOT NULL REFERENCES parking_slots(id) ON DELETE CASCADE,
    reservation_id UUID REFERENCES reservations(id) ON DELETE SET NULL,
    parking_session_id UUID NULL, -- Được liên kết khi xe vào thực tế
    allocated_period TSTZRANGE NOT NULL,
    allocation_mode VARCHAR(50) NOT NULL, -- 'HARD_PREBOOKED', 'DYNAMIC_CHECKIN', 'WALKIN_DISPATCHED'
    allocation_status VARCHAR(50) NOT NULL DEFAULT 'RESERVED', -- 'RESERVED', 'OCCUPIED', 'RELEASED'
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT no_overlapping_slot_allocations EXCLUDE USING gist (
        slot_id WITH =,
        allocated_period WITH &&
    ) WHERE (allocation_status IN ('RESERVED', 'OCCUPIED'))
);
CREATE INDEX idx_slot_allocations_slot ON slot_allocations (slot_id);
CREATE INDEX idx_slot_allocations_reservation ON slot_allocations (reservation_id);
CREATE INDEX idx_slot_allocations_session ON slot_allocations (parking_session_id);

-- 5.4. BẢNG PHIÊN ĐỖ XE THỰC TẾ (PARKING SESSIONS - TẦNG 3: REALITY)
CREATE TABLE parking_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    reservation_id UUID REFERENCES reservations(id) ON DELETE SET NULL,
    access_item_id UUID REFERENCES access_items(id),
    account_id UUID REFERENCES accounts(id), -- Nullable nếu là khách vãng lai hoàn toàn
    current_slot_id UUID REFERENCES parking_slots(id),
    plate_number VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'SEDAN',
    entry_time TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    exit_time TIMESTAMPTZ,
    session_status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'PENDING_PAYMENT', 'COMPLETED', 'OVERSTAYED', 'DISPUTED'
    entry_gate_lane VARCHAR(50),
    exit_gate_lane VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_sessions_active ON parking_sessions (site_id, session_status) WHERE (session_status = 'ACTIVE');
CREATE INDEX idx_sessions_plate ON parking_sessions (site_id, plate_number);

-- Bổ sung Foreign Key hai chiều an toàn giữa access_items và parking_sessions
ALTER TABLE access_items 
ADD CONSTRAINT fk_access_item_current_session 
FOREIGN KEY (current_session_id) REFERENCES parking_sessions(id) ON DELETE SET NULL;

-- ======================================================================================
-- MODULE 6: E-COMMERCE PAYMENT & BILLING (MÔ HÌNH ĐƠN THANH TOÁN CHUẨN THƯƠNG MẠI)
-- ======================================================================================

-- 6.1. BẢNG ĐƠN HÀNG THANH TOÁN THỐNG NHẤT (PAYMENT ORDERS / INTENTS)
-- Gom toàn bộ lý do thu tiền (Cọc booking, Tiền giờ đỗ xe, Tiền phạt) về một mô hình chuẩn
CREATE TABLE payment_orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    account_id UUID REFERENCES accounts(id), -- Người chịu trách nhiệm trả
    order_code VARCHAR(100) NOT NULL UNIQUE, -- vd: 'ORD-20261005-0001'
    order_type VARCHAR(50) NOT NULL, -- 'RESERVATION_DEPOSIT', 'PARKING_SETTLEMENT', 'VIOLATION_FINE', 'LOST_TICKET_FEE'
    reference_type VARCHAR(50) NOT NULL, -- 'RESERVATION', 'PARKING_SESSION', 'DISPUTE'
    reference_id UUID NOT NULL, -- ID của reservation hoặc parking_session tương ứng
    amount DECIMAL(12,2) NOT NULL,
    discount_amount DECIMAL(12,2) NOT NULL DEFAULT 0,
    deposit_deducted DECIMAL(12,2) NOT NULL DEFAULT 0,
    final_amount DECIMAL(12,2) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'PAID', 'CANCELLED', 'REFUNDED', 'PARTIALLY_REFUNDED'
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_payment_orders_reference ON payment_orders (reference_type, reference_id);
CREATE INDEX idx_payment_orders_account ON payment_orders (account_id);
CREATE INDEX idx_payment_orders_status ON payment_orders (status);

-- 6.2. BẢNG GIAO DỊCH DÒNG TIỀN (PAYMENT TRANSACTIONS)
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    transaction_type VARCHAR(50) NOT NULL, -- 'PAYMENT', 'REFUND'
    payment_method VARCHAR(50) NOT NULL, -- 'CASH', 'VNPAY', 'MOMO', 'CREDIT_CARD', 'WALLET'
    gateway_transaction_id VARCHAR(255),
    amount DECIMAL(12,2) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'SUCCESS', 'FAILED'
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_payment_transactions_order ON payment_transactions (payment_order_id);

-- 6.3. BẢNG HÓA ĐƠN KẾ TOÁN BẤT BIẾN (INVOICES - XUẤT SAU KHI SETTLEMENT XONG)
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id UUID NOT NULL REFERENCES parking_sessions(id) ON DELETE RESTRICT,
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT,
    invoice_number VARCHAR(100) NOT NULL UNIQUE,
    total_parked_minutes INT NOT NULL,
    base_parking_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    overstay_penalty_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(12,2) NOT NULL DEFAULT 0,
    deposit_deducted DECIMAL(12,2) NOT NULL DEFAULT 0,
    final_amount DECIMAL(12,2) NOT NULL,
    pricing_snapshot JSONB NOT NULL, -- Line-items chi tiết đóng băng tại thời điểm xuất hóa đơn
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_invoices_session ON invoices (session_id);
CREATE INDEX idx_invoices_order ON invoices (payment_order_id);

-- ======================================================================================
-- MODULE 7: DISPUTES & APPEALS (KHIẾU NẠI & TRANH CHẤP CHUẨN 4 BƯỚC)
-- ======================================================================================

-- 7.1. BẢNG KHIẾU NẠI CỦA KHÁCH HÀNG (DISPUTES)
CREATE TABLE disputes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL REFERENCES parking_sites(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    session_id UUID REFERENCES parking_sessions(id) ON DELETE SET NULL,
    reservation_id UUID REFERENCES reservations(id) ON DELETE SET NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    evidence_urls JSONB, -- Danh sách hình ảnh, chứng từ tải lên
    status VARCHAR(50) NOT NULL DEFAULT 'SUBMITTED', -- 'SUBMITTED', 'UNDER_REVIEW', 'ACCEPTED', 'REJECTED'
    resolution_notes TEXT,
    resolved_by UUID REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resolved_at TIMESTAMPTZ
);
CREATE INDEX idx_disputes_account ON disputes (account_id);
CREATE INDEX idx_disputes_site_status ON disputes (site_id, status);

-- ======================================================================================
-- MODULE 8: AUDIT LOGGING (KIỂM TOÁN VẬN HÀNH BẤT BIẾN)
-- ======================================================================================

-- 8.1. BẢNG NHẬT KÝ KIỂM TOÁN HỆ THỐNG (AUDIT LOGS)
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    site_id UUID REFERENCES parking_sites(id) ON DELETE SET NULL,
    actor_id UUID REFERENCES users(id) ON DELETE SET NULL,
    actor_role VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL, -- 'MANUAL_BARRIER_OPEN', 'FINE_WAIVED', 'SLOT_OVERRIDE', 'PRICE_CHANGED'
    target_entity VARCHAR(100) NOT NULL,
    target_id UUID,
    before_state JSONB,
    after_state JSONB,
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_audit_logs_actor ON audit_logs (actor_id);
CREATE INDEX idx_audit_logs_site_action ON audit_logs (site_id, action);
