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
    email_verified_at TIMESTAMPTZ,
    phone_verified_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL', 'PENDING_VERIFICATION', 'REJECTED')),
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
    
    permissions TEXT[],
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
INSERT INTO roles (code, name) VALUES 
('ADMIN', 'System Administrator'),
('DRIVER', 'Driver'),
('OWNER', 'Owner'),
('OPERATOR', 'Site Operator / Attendant') 
ON CONFLICT DO NOTHING;

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
    original_plate VARCHAR(30) NOT NULL,
    normalized_plate VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    image_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE INDEX ix_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;
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
    CHECK (cardinality(permissions) BETWEEN 1 AND 7),
    CHECK (array_position(permissions, NULL) IS NULL),
    CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW','SLOT_OVERRIDE','EMERGENCY_GATE_RELEASE','VIOLATION_REVIEW']::TEXT[])
);
CREATE INDEX idx_operator_grants_creator ON operator_grants(created_by);

-- 10. BẢNG XÁC THỰC (AUTH CHALLENGES & MFA)
CREATE TABLE auth_challenges (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    purpose TEXT NOT NULL,
    channel TEXT NOT NULL,
    destination TEXT NOT NULL,
    code_hash TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    resend_at TIMESTAMPTZ NOT NULL,
    attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    consumed_at TIMESTAMPTZ
);
CREATE INDEX ix_auth_challenge_user ON auth_challenges(user_id, purpose);

CREATE TABLE workflow_deliveries (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    actor_id UUID NOT NULL REFERENCES users(id),
    kind TEXT NOT NULL,
    protected_payload TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',
    attempts INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL,
    sent_at TIMESTAMPTZ,
    retry_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    challenge_id UUID REFERENCES auth_challenges(id),
    CHECK(status IN ('pending','failed','sent','cancelled'))
);
CREATE INDEX ix_workflow_delivery_due ON workflow_deliveries(status, retry_at);

CREATE TABLE mfa_credentials (
    user_id UUID PRIMARY KEY REFERENCES users(id),
    protected_secret TEXT NOT NULL,
    enabled BOOL NOT NULL DEFAULT FALSE,
    last_step BIGINT NOT NULL DEFAULT -1,
    attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ
);
