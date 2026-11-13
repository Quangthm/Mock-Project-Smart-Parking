-- Canonical UserDb schema. CREATE IF NOT EXISTS permits replay on an aligned schema.
-- This is not an upgrade: existing tables are NOT reshaped or silently repaired.
-- Explicit exceptions: circular media foreign keys below, the existing session-binding
-- immutability trigger, and the idempotent fixed role catalogue seed.
BEGIN;
-- ======================================================================================
-- MICROSERVICE: USER & ACCESS SERVICE DATABASE (UserDb)
-- Domain: Identity, Authentication, TOTP, RBAC, Legal Agreements, Tenants, Vehicles
-- Message Broker: RabbitMQ + MassTransit
-- Architectural Standard: Clean Architecture / DDD v1.0 RC3
-- ======================================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. BẢNG ĐỊNH DANH CON NGƯỜI (HUMAN IDENTITY)
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone VARCHAR(20),
    email VARCHAR(255),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    company_name VARCHAR(255),
    avatar_url TEXT,
    avatar_asset_id UUID,
    simulated_vneid_guid UUID,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    access_version BIGINT NOT NULL DEFAULT 0,
    email_verified_at TIMESTAMPTZ,
    phone_verified_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL', 'PENDING_VERIFICATION', 'REJECTED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_user_phone ON users (phone) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_user_email_normalized ON users (lower(email)) WHERE deleted_at IS NULL AND email IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_users_status ON users (status);

-- 2. BẢNG DOANH NGHIỆP / TỔ CHỨC (TENANTS)
-- Quyền sở hữu chính thức thuộc về User & Access Service theo kiến trúc v1.0
CREATE TABLE IF NOT EXISTS tenants (
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
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_tenant_code ON tenants (code) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_tenants_status ON tenants (status);

-- 3. BẢNG DANH MỤC VAI TRÒ (ROLES)
CREATE TABLE IF NOT EXISTS roles (
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
('OPERATOR', 'Site Operator / Attendant'),
('BUSINESS_OWNER', 'Business Owner'),
('SITE_OPERATOR', 'Site Operator'),
('PLATFORM_ADMIN', 'Platform Administrator')
ON CONFLICT DO NOTHING;
-- Runtime aliases remain supported by the existing Owner/Operator/Admin workflows.

-- 4. BẢNG TÀI KHOẢN NGƯỜI DÙNG PHÂN THEO NGỮ CẢNH (ACCOUNTS)
CREATE TABLE IF NOT EXISTS accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    -- Cross-domain Logical Reference to FacilityDb
    site_id UUID,

    permissions TEXT[],
    status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL', 'PENDING_VERIFICATION')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "AK_accounts_id_user_id" UNIQUE (id, user_id),
    deleted_at TIMESTAMPTZ
);


CREATE INDEX IF NOT EXISTS idx_accounts_user_id ON accounts (user_id);
CREATE INDEX IF NOT EXISTS idx_accounts_role_code ON accounts (role_code);
CREATE INDEX IF NOT EXISTS idx_accounts_tenant_id ON accounts (tenant_id);
CREATE INDEX IF NOT EXISTS idx_accounts_site_id ON accounts (site_id);
CREATE INDEX IF NOT EXISTS idx_accounts_status ON accounts (status);

-- 6. BẢNG PHƯƠNG TIỆN (VEHICLES)
CREATE TABLE IF NOT EXISTS vehicles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID REFERENCES accounts(id) ON DELETE SET NULL,
    original_plate VARCHAR(30) NOT NULL,
    normalized_plate VARCHAR(20) NOT NULL,
    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    image_url TEXT,
    image_asset_id UUID,
    binding_revision BIGINT NOT NULL DEFAULT 1 CHECK (binding_revision > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_vehicles_account_id ON vehicles (account_id);
CREATE INDEX IF NOT EXISTS idx_vehicles_type ON vehicles (vehicle_type);

-- 7. BẢNG QUẢN LÝ REFRESH TOKEN (SESSION TOKENS)
CREATE TABLE IF NOT EXISTS user_refresh_tokens (
    account_id UUID NULL,
    CONSTRAINT "FK_user_refresh_tokens_accounts_account_id_user_id" FOREIGN KEY (account_id, user_id) REFERENCES accounts(id, user_id) ON DELETE RESTRICT,
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    device_info VARCHAR(255),
    ip_address VARCHAR(45),
    expires_at TIMESTAMPTZ NOT NULL,
    access_token_id UUID NOT NULL UNIQUE,
    access_expires_at TIMESTAMPTZ NOT NULL,
    last_refreshed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_expires_at_valid CHECK (expires_at > created_at)
);
CREATE INDEX IF NOT EXISTS "IX_user_refresh_tokens_account_id_user_id" ON user_refresh_tokens(account_id, user_id);
CREATE INDEX IF NOT EXISTS idx_user_refresh_tokens_user_id ON user_refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS idx_user_refresh_tokens_expires_at ON user_refresh_tokens (expires_at);

CREATE TABLE IF NOT EXISTS rotated_session_proofs (
    access_token_id UUID PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES user_refresh_tokens(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL,
    access_expires_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_rotated_session_proofs_session ON rotated_session_proofs(session_id);
CREATE INDEX IF NOT EXISTS idx_rotated_session_proofs_user ON rotated_session_proofs(user_id);

-- 8. BẢNG ĐĂNG KÝ TÀI XẾ (DRIVER REGISTRATION OTP)
CREATE TABLE IF NOT EXISTS driver_registrations (
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
CREATE INDEX IF NOT EXISTS idx_driver_registrations_user ON driver_registrations(user_id);

-- 9. BẢNG ĐIỀU KHOẢN VÀ THỎA THUẬN DỊCH VỤ (SYSTEM TERMS & CONDITIONS)
CREATE TABLE IF NOT EXISTS system_terms_and_conditions (
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
CREATE INDEX IF NOT EXISTS idx_system_terms_active ON system_terms_and_conditions (term_code, is_active);

-- 10. BẢNG ĐƠN ĐĂNG KÝ CHỦ BÃI XE (OWNER APPLICATIONS)
CREATE TABLE IF NOT EXISTS owner_applications (
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
    lot_setup JSONB,
    setup_token_hash TEXT,
    setup_token_expires_at TIMESTAMPTZ,
    approval_requested_by UUID REFERENCES users(id),
    
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    submitted_at TIMESTAMPTZ NOT NULL,
    reviewed_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES users(id),
    review_note VARCHAR(2000),
    CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL)
        OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS idx_owner_applications_user_id ON owner_applications(user_id);
CREATE INDEX IF NOT EXISTS idx_owner_applications_tenant_id ON owner_applications(tenant_id);
CREATE INDEX IF NOT EXISTS idx_owner_applications_terms_id ON owner_applications(terms_id);
CREATE INDEX IF NOT EXISTS idx_owner_applications_status ON owner_applications(status);

-- 11. BẢNG PHÂN QUYỀN VẬN HÀNH BÃI (OPERATOR GRANTS - 7 PERMISSIONS)
CREATE TABLE IF NOT EXISTS operator_grants (
    account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    permissions TEXT[] NOT NULL,
    CHECK (cardinality(permissions) BETWEEN 1 AND 7),
    CHECK (array_position(permissions, NULL) IS NULL),
    CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW','SLOT_OVERRIDE','EMERGENCY_GATE_RELEASE','VIOLATION_REVIEW']::TEXT[])
);
CREATE INDEX IF NOT EXISTS idx_operator_grants_creator ON operator_grants(created_by);

-- 12. BẢNG XÁC THỰC THÁCH THỨC (AUTH CHALLENGES & MFA)
CREATE TABLE IF NOT EXISTS auth_challenges (
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
CREATE INDEX IF NOT EXISTS ix_auth_challenge_user ON auth_challenges(user_id, purpose);

-- 13. BẢNG CHUYỂN PHÁT QUY TRÌNH XÁC THỰC (WORKFLOW DELIVERIES)
CREATE TABLE IF NOT EXISTS workflow_deliveries (
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
CREATE INDEX IF NOT EXISTS ix_workflow_delivery_due ON workflow_deliveries(status, retry_at);
CREATE INDEX IF NOT EXISTS idx_workflow_deliveries_user_id ON workflow_deliveries(user_id);
CREATE INDEX IF NOT EXISTS idx_workflow_deliveries_challenge_id ON workflow_deliveries(challenge_id);

-- 14. BẢNG THÔNG TIN XÁC THỰC ĐA YẾU TỐ (MFA/TOTP CREDENTIALS)
CREATE TABLE IF NOT EXISTS mfa_credentials (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    protected_secret TEXT NOT NULL,
    enabled BOOL NOT NULL DEFAULT FALSE,
    last_step BIGINT NOT NULL DEFAULT -1,
    attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    pending_protected_secret TEXT,
    pending_expires_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS mfa_recovery_codes (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    code_hash TEXT NOT NULL,
    used_at TIMESTAMPTZ,
    UNIQUE (user_id, code_hash)
);

CREATE INDEX IF NOT EXISTS idx_mfa_recovery_codes_user ON mfa_recovery_codes(user_id);

-- Retained by explicit instruction: a session may never be retargeted to another
-- Account/User, including by non-EF writers. The composite FK validates ownership;
-- this existing trigger additionally enforces immutability across updates.
CREATE OR REPLACE FUNCTION protect_auth_session_account_binding() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.account_id IS DISTINCT FROM OLD.account_id OR NEW.user_id IS DISTINCT FROM OLD.user_id THEN
        RAISE EXCEPTION 'Session account binding is immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
CREATE OR REPLACE TRIGGER tr_auth_session_account_binding BEFORE UPDATE OF account_id, user_id ON user_refresh_tokens
    FOR EACH ROW EXECUTE FUNCTION protect_auth_session_account_binding();

-- Durable per-attempt Account-current decision and replay identity; no browser-session continuity implied.
-- Reservation first slice: user-approved one persisted Account-current result per workflow.
-- Retries replay that result; attempt IDs correlate transport delivery, not fresh evaluation.
-- request_fingerprint binds actor/operation/original deadline to the workflow identity.
-- This is not an audit sink, booking aggregate, session or reusable authorization grant.
-- No cleanup is implemented; retain evidence until an owner-approved replay/retention design.
CREATE TABLE IF NOT EXISTS account_access_decisions (
    id UUID PRIMARY KEY,
    workflow_id UUID NOT NULL UNIQUE,
    user_id UUID NOT NULL,
    account_id UUID NOT NULL,
    role TEXT NOT NULL,
    operation TEXT NOT NULL,
    request_fingerprint TEXT NOT NULL,
    allowed BOOLEAN NOT NULL,
    decided_at TIMESTAMPTZ NOT NULL
);

-- BEGIN FRAMEWORK DDL
CREATE TABLE IF NOT EXISTS "InboxState" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MessageId" uuid NOT NULL,
    "ConsumerId" uuid NOT NULL,
    "LockId" uuid NOT NULL,
    "RowVersion" bytea,
    "Received" timestamp with time zone NOT NULL,
    "ReceiveCount" integer NOT NULL,
    "ExpirationTime" timestamp with time zone,
    "Consumed" timestamp with time zone,
    "Delivered" timestamp with time zone,
    "LastSequenceNumber" bigint,
    CONSTRAINT "PK_InboxState" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_InboxState_MessageId_ConsumerId" UNIQUE ("MessageId", "ConsumerId")
);


CREATE TABLE IF NOT EXISTS "OutboxState" (
    "OutboxId" uuid NOT NULL,
    "LockId" uuid NOT NULL,
    "RowVersion" bytea,
    "Created" timestamp with time zone NOT NULL,
    "Delivered" timestamp with time zone,
    "LastSequenceNumber" bigint,
    CONSTRAINT "PK_OutboxState" PRIMARY KEY ("OutboxId")
);


CREATE TABLE IF NOT EXISTS "OutboxMessage" (
    "SequenceNumber" bigint GENERATED BY DEFAULT AS IDENTITY,
    "EnqueueTime" timestamp with time zone,
    "SentTime" timestamp with time zone NOT NULL,
    "Headers" text,
    "Properties" text,
    "InboxMessageId" uuid,
    "InboxConsumerId" uuid,
    "OutboxId" uuid,
    "MessageId" uuid NOT NULL,
    "ContentType" character varying(256) NOT NULL,
    "MessageType" text NOT NULL,
    "Body" text NOT NULL,
    "ConversationId" uuid,
    "CorrelationId" uuid,
    "InitiatorId" uuid,
    "RequestId" uuid,
    "SourceAddress" character varying(256),
    "DestinationAddress" character varying(256),
    "ResponseAddress" character varying(256),
    "FaultAddress" character varying(256),
    "ExpirationTime" timestamp with time zone,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("SequenceNumber"),
    CONSTRAINT "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId" FOREIGN KEY ("InboxMessageId", "InboxConsumerId") REFERENCES "InboxState" ("MessageId", "ConsumerId"),
    CONSTRAINT "FK_OutboxMessage_OutboxState_OutboxId" FOREIGN KEY ("OutboxId") REFERENCES "OutboxState" ("OutboxId")
);


CREATE INDEX IF NOT EXISTS "IX_InboxState_Delivered" ON "InboxState" ("Delivered");


CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_EnqueueTime" ON "OutboxMessage" ("EnqueueTime");


CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_ExpirationTime" ON "OutboxMessage" ("ExpirationTime");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber" ON "OutboxMessage" ("InboxMessageId", "InboxConsumerId", "SequenceNumber");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_OutboxMessage_OutboxId_SequenceNumber" ON "OutboxMessage" ("OutboxId", "SequenceNumber");


CREATE INDEX IF NOT EXISTS "IX_OutboxState_Created" ON "OutboxState" ("Created");


-- END FRAMEWORK DDL
-- SPARK-373 additive
-- Media schema: declarations only, no appended upgrade/column alteration.
CREATE TABLE IF NOT EXISTS media_assets (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id),
    creator_account_id uuid NOT NULL,
    scope_id uuid NOT NULL,
    purpose text NOT NULL CHECK (purpose IN ('avatar','vehicle')),
    sensitivity text NOT NULL DEFAULT 'restricted' CHECK (sensitivity = 'restricted'),
    vehicle_id uuid REFERENCES vehicles(id),
    registration_claim_id uuid,
    draft_intent_id uuid,
    idempotency_key text NOT NULL CHECK (length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL,
    mime_type text NOT NULL CHECK (mime_type IN ('image/jpeg','image/png','image/webp')),
    byte_count bigint NOT NULL CHECK (byte_count BETWEEN 1 AND 5242880),
    sha256 text NOT NULL CHECK (length(sha256) = 64),
    width integer NOT NULL CHECK (width BETWEEN 1 AND 8192),
    height integer NOT NULL CHECK (height BETWEEN 1 AND 8192 AND width::bigint * height <= 25000000),
    provider text NOT NULL,
    environment text NOT NULL,
    public_id text NOT NULL,
    resource_type text NOT NULL CHECK (resource_type = 'image'),
    delivery_type text NOT NULL CHECK (delivery_type = 'authenticated'),
    provider_asset_id text,
    provider_version text,
    state text NOT NULL DEFAULT 'pending' CHECK (state IN ('pending','ready')),
    version bigint NOT NULL DEFAULT 0,
    admitted_at timestamptz NOT NULL,
    draft_expires_at timestamptz NOT NULL,
    retention_class text NOT NULL CHECK (retention_class = 'image-30-days'),
    retention_anchor timestamptz NOT NULL,
    retain_until timestamptz NOT NULL,
    ready_at timestamptz,
    attached_at timestamptz,
    lease_until timestamptz,
    claim_id uuid,
    attempts integer NOT NULL DEFAULT 0,
    retry_at timestamptz,
    recovery_code text,
    CONSTRAINT fk_media_creator FOREIGN KEY (creator_account_id,user_id) REFERENCES accounts(id,user_id),
    CONSTRAINT ck_media_scope CHECK (scope_id = creator_account_id),
    CONSTRAINT ck_media_purpose_target CHECK ((purpose = 'avatar' AND vehicle_id IS NULL AND draft_intent_id IS NULL)
        OR (purpose = 'vehicle' AND (vehicle_id IS NOT NULL OR draft_intent_id IS NOT NULL))),
    CONSTRAINT ck_media_claim_target CHECK (registration_claim_id IS NULL OR
        (vehicle_id IS NULL AND purpose='vehicle' AND attached_at IS NOT NULL)),
    CONSTRAINT ck_media_ready CHECK (state <> 'ready' OR (provider_asset_id IS NOT NULL AND provider_version IS NOT NULL AND ready_at IS NOT NULL)),
    CONSTRAINT ck_media_deadline CHECK (retention_anchor = admitted_at AND retain_until = admitted_at + interval '30 days'
        AND draft_expires_at = admitted_at + interval '24 hours')
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_media_upload_key ON media_assets(scope_id,purpose,idempotency_key);
CREATE UNIQUE INDEX IF NOT EXISTS uq_media_provider_locator ON media_assets(provider,environment,public_id,resource_type,delivery_type);
CREATE INDEX IF NOT EXISTS idx_media_pending ON media_assets(state,retry_at,lease_until);
CREATE INDEX IF NOT EXISTS idx_media_due ON media_assets(retain_until);
CREATE TABLE IF NOT EXISTS media_holds (
    id uuid PRIMARY KEY,
    asset_id uuid NOT NULL REFERENCES media_assets(id),
    originating_hold_id text NOT NULL,
    actor_account_id uuid NOT NULL REFERENCES accounts(id),
    reason text NOT NULL,
    reference text NOT NULL,
    placed_at timestamptz NOT NULL,
    acknowledged_at timestamptz,
    review_at timestamptz,
    released_at timestamptz,
    UNIQUE (asset_id, originating_hold_id)
);
-- Private registration intake is independent of active vehicle ownership.
CREATE TABLE IF NOT EXISTS vehicle_registration_claims (
    id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    original_plate varchar(30) NOT NULL,
    normalized_plate varchar(20) NOT NULL,
    vehicle_type text NOT NULL CHECK (vehicle_type IN ('CAR','MOTORCYCLE')),
    image_asset_id uuid NOT NULL REFERENCES media_assets(id) ON DELETE RESTRICT,
    status text NOT NULL CHECK (status='PENDING_DISPUTE'),
    created_at timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_pending_vehicle_claim
    ON vehicle_registration_claims(account_id,normalized_plate) WHERE status='PENDING_DISPUTE';
-- Ensure existing pre-media databases have the nullable media references.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS avatar_asset_id UUID;

ALTER TABLE vehicles
    ADD COLUMN IF NOT EXISTS image_asset_id UUID;
-- Circular dependencies only: users/vehicles/claims select their current photo,
-- while media owns its creator and target references. These FKs must be added after
-- both tables exist. Catalog guards make repeated execution idempotent.
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='users'::regclass AND conname='fk_users_avatar_asset') THEN
        ALTER TABLE users ADD CONSTRAINT fk_users_avatar_asset FOREIGN KEY (avatar_asset_id) REFERENCES media_assets(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='vehicles'::regclass AND conname='fk_vehicles_image_asset') THEN
        ALTER TABLE vehicles ADD CONSTRAINT fk_vehicles_image_asset FOREIGN KEY (image_asset_id) REFERENCES media_assets(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='media_assets'::regclass AND conname='fk_media_registration_claim') THEN
        ALTER TABLE media_assets ADD CONSTRAINT fk_media_registration_claim FOREIGN KEY (registration_claim_id)
            REFERENCES vehicle_registration_claims(id) ON DELETE RESTRICT;
    END IF;
END $$;
-- END SPARK-373 media schema

-- ======================================================================================
-- VNeID SIMULATION PERSISTENCE (V11-06 / V11-DES-02 / DB26)
-- ======================================================================================
CREATE TABLE IF NOT EXISTS user_simulated_identities (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    simulated_vneid_guid UUID NOT NULL,
    consent_given BOOLEAN NOT NULL DEFAULT TRUE,
    consent_timestamp TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    consent_version VARCHAR(50) NOT NULL,
    simulation_status VARCHAR(30) NOT NULL DEFAULT 'COMPLETED',
    mock_document_number VARCHAR(50),
    mock_full_name VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_user_simulated_identities_user_id ON user_simulated_identities(user_id);
CREATE INDEX IF NOT EXISTS idx_user_simulated_identities_guid ON user_simulated_identities(simulated_vneid_guid);

COMMIT;
