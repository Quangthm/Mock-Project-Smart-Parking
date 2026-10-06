CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone VARCHAR(20),
    email VARCHAR(255),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    company_name VARCHAR(255),
    avatar_url TEXT,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id UUID, 
    site_id UUID, 
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE TABLE roles (
    code VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE account_roles (
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, role_code)
);
CREATE TABLE vehicles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID REFERENCES accounts(id) ON DELETE SET NULL,
    original_plate VARCHAR(20) NOT NULL,
    normalized_plate VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    image_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE TABLE user_refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    device_info VARCHAR(255),
    ip_address VARCHAR(45),
    expires_at TIMESTAMPTZ NOT NULL,
    access_token_id UUID NOT NULL UNIQUE,
    access_expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_expires_at_valid CHECK (expires_at > created_at)
);
CREATE TABLE driver_registrations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    channel VARCHAR(10) NOT NULL CHECK (channel IN ('email', 'sms')),
    code_hash TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    resend_available_at TIMESTAMPTZ NOT NULL,
    failed_attempts INTEGER NOT NULL DEFAULT 0 CHECK (failed_attempts BETWEEN 0 AND 3),
    locked_until TIMESTAMPTZ,
    verified_at TIMESTAMPTZ
);
CREATE TABLE owner_applications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    business_name VARCHAR(255) NOT NULL,
    lot_type VARCHAR(30) NOT NULL CHECK (lot_type IN ('outdoor', 'basement', 'multi-storey')),
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    submitted_at TIMESTAMPTZ NOT NULL,
    reviewed_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES users(id),
    review_note VARCHAR(2000),
    CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL)
        OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))
);
CREATE TABLE operator_grants (
    account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    permissions TEXT[] NOT NULL,
    CHECK (cardinality(permissions) BETWEEN 1 AND 4),
    CHECK (array_position(permissions, NULL) IS NULL),
    CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW']::TEXT[])
);

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
CREATE TABLE parking_slots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    spatial_unit_id UUID NOT NULL,
    slot_code VARCHAR(50) NOT NULL,
    supported_vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (supported_vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
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
CREATE TABLE access_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    site_id UUID NOT NULL,
    item_type VARCHAR(50) NOT NULL CHECK (item_type IN ('CARD', 'QR', 'LICENSE_PLATE', 'RFID')),
    identifier_code VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (status IN ('AVAILABLE', 'IN_USE', 'LOST', 'DISABLED')),
    current_session_id UUID NULL, 
    issued_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, identifier_code),
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE
);
CREATE TABLE site_operational_settings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    site_id UUID,
    free_parking_grace_minutes INT NOT NULL DEFAULT 15,
    arrival_late_tolerance_minutes INT NOT NULL DEFAULT 15,
    payment_hold_duration_minutes INT NOT NULL DEFAULT 5, -- SRS Default: 5 phút
    min_reservation_duration_minutes INT NOT NULL DEFAULT 30,
    max_reservation_duration_hours INT NOT NULL DEFAULT 24,
    reservation_protection_window_hours INT NOT NULL DEFAULT 4,
    allocation_lead_time_minutes INT NOT NULL DEFAULT 30,
    occupancy_validity_timespan_hours INT NOT NULL DEFAULT 4, -- SRS Default: 4 giờ
    version INT NOT NULL DEFAULT 1,
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
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    version INT NOT NULL DEFAULT 1,
    tariff_rules JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (site_id, tenant_id) REFERENCES parking_sites(id, tenant_id) ON DELETE CASCADE
);
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

CREATE TABLE site_capacity_pools (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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
CREATE TABLE reservations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID NOT NULL, 
    target_spatial_unit_id UUID,
    target_slot_id UUID,
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
CREATE TABLE parking_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    access_item_id UUID,
    account_id UUID,
    current_slot_id UUID,
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
CREATE TABLE slot_allocations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID,
    parking_session_id UUID,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL,
    allocated_start_time TIMESTAMPTZ NOT NULL,
    allocated_end_time TIMESTAMPTZ NOT NULL,
    allocation_status VARCHAR(50) NOT NULL DEFAULT 'RESERVED' CHECK (allocation_status IN ('RESERVED', 'OCCUPIED', 'RELEASED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (allocated_end_time > allocated_start_time),
    FOREIGN KEY (reservation_id, tenant_id, site_id) REFERENCES reservations(id, tenant_id, site_id) ON DELETE SET NULL,
    FOREIGN KEY (parking_session_id, tenant_id, site_id) REFERENCES parking_sessions(id, tenant_id, site_id) ON DELETE SET NULL
);

CREATE TABLE payment_orders (
    id UUID DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    account_id UUID,
    order_code VARCHAR(100) NOT NULL UNIQUE,
    idempotency_key VARCHAR(255) UNIQUE,
    order_type VARCHAR(50) NOT NULL CHECK (order_type IN ('PARKING_FEE', 'MONTHLY_PASS_FEE', 'OVERNIGHT_FEE', 'RESERVATION_FEE')), 
    reference_type VARCHAR(50) NOT NULL CHECK (reference_type IN ('RESERVATION', 'PARKING_SESSION')), -- 'RESERVATION', 'PARKING_SESSION'
    reference_id UUID NOT NULL, -- Logical ID trỏ sang DB Reservation
    amount DECIMAL(12,2) NOT NULL CHECK (amount >= 0),
    discount_amount DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    deposit_deducted DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (deposit_deducted >= 0),
    final_amount DECIMAL(12,2) NOT NULL CHECK (final_amount >= 0),
    CHECK (final_amount = (amount - discount_amount - deposit_deducted)),
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED')),
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE (id, tenant_id) -- For RLS Composite FKs
);
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    transaction_type VARCHAR(50) NOT NULL CHECK (transaction_type IN ('PAYMENT', 'REFUND')), -- 'PAYMENT', 'REFUND'
    payment_method VARCHAR(50) NOT NULL CHECK (payment_method IN ('VNPAY', 'MOMO', 'ZALOPAY', 'CASH')), -- 'VNPAY', 'MOMO', 'ZALOPAY', 'CASH'
    gateway_transaction_id VARCHAR(255),
    amount DECIMAL(12,2) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'UNKNOWN')),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    session_id UUID NOT NULL,
    site_id UUID NOT NULL,
    invoice_number VARCHAR(100) NOT NULL UNIQUE,
    total_parked_minutes INT NOT NULL,
    base_parking_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    overstay_penalty_fee DECIMAL(12,2) NOT NULL DEFAULT 0,
    final_amount DECIMAL(12,2) NOT NULL,
    pricing_snapshot JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);
CREATE TABLE refund_requests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    payment_order_id UUID NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    reason TEXT NOT NULL,
    evidence_url VARCHAR(255),
    status VARCHAR(50) NOT NULL DEFAULT 'REFUND_REQUESTED' CHECK (status IN ('REFUND_REQUESTED', 'REFUND_APPROVED', 'REFUND_SUBMITTED', 'REFUND_COMPLETED', 'REJECTED')), -- REFUND_REQUESTED, REFUND_APPROVED, REFUND_SUBMITTED, REFUND_COMPLETED, REJECTED
    approved_by UUID, -- Operator or Owner ID
    handled_by_owner BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT
);
CREATE TABLE provider_webhooks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    provider VARCHAR(50) NOT NULL,
    provider_event_id VARCHAR(255) NOT NULL,
    processing_state VARCHAR(50) NOT NULL DEFAULT 'PENDING' CHECK (processing_state IN ('PENDING', 'PROCESSED', 'FAILED')),
    payload JSONB,
    received_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (provider, provider_event_id)
);

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
CREATE TABLE push_device_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL,
    device_token VARCHAR(255) NOT NULL, -- FCM Token / APNS Token
    device_os VARCHAR(50) CHECK (device_os IN ('IOS', 'ANDROID', 'WEB')), -- 'IOS', 'ANDROID', 'WEB'
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_used_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE notification_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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

CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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
CREATE TABLE lpr_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
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
CREATE TABLE sensor_telemetries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    slot_id UUID NOT NULL, -- Logical ID trỏ sang Parking Service
    is_occupied BOOLEAN NOT NULL,
    detected_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (device_id, tenant_id, site_id) REFERENCES devices(id, tenant_id, site_id) ON DELETE CASCADE
);


