-- ======================================================================================
-- MICROSERVICE: USER & ACCESS SERVICE DATABASE (UserDb)
-- Domain: Identity, Authentication, TOTP, RBAC, Legal Agreements, Tenants, Vehicles
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG ĐỊNH DANH CON NGƯỜI (HUMAN IDENTITY)
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
CREATE INDEX idx_users_status ON users (status);

-- 2. BẢNG DOANH NGHIỆP / TỔ CHỨC (TENANTS)
-- Quyền sở hữu chính thức thuộc về User & Access Service theo kiến trúc v1.0
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    contact_email VARCHAR(255) NOT NULL,
    contact_phone VARCHAR(20) NOT NULL,
    tax_code VARCHAR(50),
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX uq_active_tenant_code ON tenants (code) WHERE deleted_at IS NULL;
CREATE INDEX idx_tenants_status ON tenants (status);

-- 3. BẢNG TÀI KHOẢN NGƯỜI DÙNG PHÂN THEO NGỮ CẢNH (ACCOUNTS)
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL, 
    -- Cross-domain Logical Reference to FacilityDb
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
CREATE INDEX idx_accounts_status ON accounts (status);

-- 4. BẢNG DANH MỤC VAI TRÒ (ROLES)
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

-- 5. BẢNG PHÂN VAI TRÒ CHO TÀI KHOẢN (ACCOUNT ROLES)
CREATE TABLE account_roles (
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, role_code)
);
CREATE INDEX idx_account_roles_role_code ON account_roles (role_code);

-- 6. BẢNG PHƯƠNG TIỆN (VEHICLES)
CREATE TABLE vehicles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID REFERENCES accounts(id) ON DELETE SET NULL,
    original_plate VARCHAR(30) NOT NULL,
    normalized_plate VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    image_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE UNIQUE INDEX uq_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;
CREATE INDEX idx_vehicles_account_id ON vehicles (account_id);
CREATE INDEX idx_vehicles_type ON vehicles (vehicle_type);

-- 7. BẢNG QUẢN LÝ REFRESH TOKEN (SESSION TOKENS)
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

-- 8. BẢNG ĐĂNG KÝ TÀI XẾ (DRIVER REGISTRATION OTP)
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
CREATE INDEX idx_driver_registrations_user ON driver_registrations(user_id);

-- 9. BẢNG ĐIỀU KHOẢN VÀ THỎA THUẬN DỊCH VỤ (SYSTEM TERMS & CONDITIONS)
CREATE TABLE system_terms_and_conditions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    term_code VARCHAR(50) NOT NULL, -- 'OWNER_ONBOARDING', 'NEW_SITE_POLICY'
    version VARCHAR(20) NOT NULL,   -- 'v1.0', 'v1.1'
    title VARCHAR(255) NOT NULL,
    document_url TEXT NOT NULL,     -- Đường dẫn file PDF/Doc trên Cloud
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (term_code, version)
);
CREATE INDEX idx_system_terms_active ON system_terms_and_conditions (term_code, is_active);

-- 10. BẢNG ĐƠN ĐĂNG KÝ CHỦ BÃI XE (OWNER APPLICATIONS)
CREATE TABLE owner_applications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    business_name VARCHAR(255) NOT NULL,
    lot_type VARCHAR(30) NOT NULL CHECK (lot_type IN ('outdoor', 'basement', 'multi-storey')),
    
    -- Điều khoản và xác thực pháp lý
    terms_id UUID REFERENCES system_terms_and_conditions(id),
    agreed_at TIMESTAMPTZ,
    agreed_ip VARCHAR(50),
    
    -- Tổ chức và cơ sở bãi xe khởi tạo ban đầu
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    site_id UUID, -- Logical ID của bãi xe ở trạng thái PENDING_APPROVAL trong FacilityDb
    draft_structure JSONB, -- Snapshot cấu hình bãi xe (Zone, Floor, Capacity) lúc đăng ký
    
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    submitted_at TIMESTAMPTZ NOT NULL,
    reviewed_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES users(id),
    review_note VARCHAR(2000),
    CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL)
        OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))
);
CREATE INDEX idx_owner_applications_user_id ON owner_applications(user_id);
CREATE INDEX idx_owner_applications_tenant_id ON owner_applications(tenant_id);
CREATE INDEX idx_owner_applications_terms_id ON owner_applications(terms_id);
CREATE INDEX idx_owner_applications_status ON owner_applications(status);

-- 11. BẢNG PHÂN QUYỀN VẬN HÀNH BÃI (OPERATOR GRANTS - 7 PERMISSIONS)
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

-- 12. BẢNG XÁC THỰC THÁCH THỨC (AUTH CHALLENGES & MFA)
CREATE TABLE auth_challenges (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
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

-- 13. BẢNG CHUYỂN PHÁT QUY TRÌNH XÁC THỰC (WORKFLOW DELIVERIES)
CREATE TABLE workflow_deliveries (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    actor_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    kind TEXT NOT NULL,
    protected_payload TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',
    attempts INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL,
    sent_at TIMESTAMPTZ,
    retry_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    challenge_id UUID REFERENCES auth_challenges(id) ON DELETE SET NULL,
    CHECK(status IN ('pending','failed','sent','cancelled'))
);
CREATE INDEX ix_workflow_delivery_due ON workflow_deliveries(status, retry_at);
CREATE INDEX idx_workflow_deliveries_user_id ON workflow_deliveries(user_id);
CREATE INDEX idx_workflow_deliveries_challenge_id ON workflow_deliveries(challenge_id);

-- 14. BẢNG THÔNG TIN XÁC THỰC ĐA YẾU TỐ (MFA/TOTP CREDENTIALS)
CREATE TABLE mfa_credentials (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    protected_secret TEXT NOT NULL,
    enabled BOOL NOT NULL DEFAULT FALSE,
    last_step BIGINT NOT NULL DEFAULT -1,
    attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ
);
