-- ======================================================================================
-- MICROSERVICE: USER SERVICE DATABASE
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG ĐỊNH DANH CON NGƯỜI
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
CREATE UNIQUE INDEX uq_active_user_phone ON users (phone) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX uq_active_user_email_normalized ON users (lower(email)) WHERE deleted_at IS NULL AND email IS NOT NULL;

-- 2. BẢNG TÀI KHOẢN NGƯỜI DÙNG PHÂN THEO NGỮ CẢNH
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    -- Cross-domain References (Logical ID)
    tenant_id UUID, 
    site_id UUID, 
    
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE INDEX idx_accounts_user_id ON accounts (user_id);
CREATE INDEX idx_accounts_tenant_id ON accounts (tenant_id);
CREATE INDEX idx_accounts_site_id ON accounts (site_id);

-- 3. BẢNG DANH MỤC VAI TRÒ
CREATE TABLE roles (
    code VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 4. BẢNG PHÂN VAI TRÒ CHO TÀI KHOẢN
CREATE TABLE account_roles (
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, role_code)
);
CREATE INDEX idx_account_roles_role_code ON account_roles (role_code);

-- 5. BẢNG PHƯƠNG TIỆN
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
CREATE UNIQUE INDEX uq_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;
CREATE INDEX idx_vehicles_account_id ON vehicles (account_id);

-- 6. BẢNG QUẢN LÝ REFRESH TOKEN
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
CREATE INDEX idx_user_refresh_tokens_user_id ON user_refresh_tokens (user_id);
CREATE INDEX idx_user_refresh_tokens_expires_at ON user_refresh_tokens (expires_at);

-- 7. BẢNG ĐĂNG KÝ TÀI XẾ (DRIVER REGISTRATION OTP)
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

-- 8. BẢNG ĐƠN ĐĂNG KÝ CHỦ BÃI XE (OWNER APPLICATIONS)
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

-- 9. BẢNG PHÂN QUYỀN VẬN HÀNH BÃI (OPERATOR GRANTS)
CREATE TABLE operator_grants (
    account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    permissions TEXT[] NOT NULL,
    CHECK (cardinality(permissions) BETWEEN 1 AND 4),
    CHECK (array_position(permissions, NULL) IS NULL),
    CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW']::TEXT[])
);
CREATE INDEX idx_operator_grants_creator ON operator_grants(created_by);
