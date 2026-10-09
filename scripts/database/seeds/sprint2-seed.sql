-- ==========================================
-- SPRINT 2 DETERMINISTIC SEED DATA
-- Applies to 01-user-service-db and 02-parking-service-db
-- ==========================================

-- >>> USER SERVICE DB SEED <<<
-- 1. Roles
INSERT INTO roles (code, name) VALUES 
('ADMIN', 'System Administrator'),
('DRIVER', 'Driver'),
('OWNER', 'Owner'),
('OPERATOR', 'Site Operator / Attendant')
ON CONFLICT DO NOTHING;

-- 2. Users (Password is 'P@ssword123' bcrypt hash)
INSERT INTO users (id, phone, email, password_hash, full_name, status, failed_login_attempts, locked_until) VALUES
('00000000-0000-0000-0000-000000000000', '0900000000', 'admin@smartpark.local', '$2a$12$K.F4m5L0dE/...', 'System Admin', 'ACTIVE', 0, NULL),
('11111111-1111-1111-1111-111111111111', '0911111111', 'owner.a@test.com', '$2a$12$K.F4m5L0dE/...', 'Owner A (Approved)', 'ACTIVE', 0, NULL),
('22222222-2222-2222-2222-222222222222', '0922222222', 'owner.b@test.com', '$2a$12$K.F4m5L0dE/...', 'Owner B (Pending)', 'PENDING_APPROVAL', 0, NULL),
('33333333-3333-3333-3333-333333333333', '0933333333', 'driver@test.com', '$2a$12$K.F4m5L0dE/...', 'Active Driver', 'ACTIVE', 0, NULL),
('44444444-4444-4444-4444-444444444444', '0944444444', 'operator@test.com', '$2a$12$K.F4m5L0dE/...', 'Operator', 'ACTIVE', 0, NULL),
('55555555-5555-5555-5555-555555555555', '0955555555', 'blocked@test.com', '$2a$12$K.F4m5L0dE/...', 'Blocked Driver', 'LOCKED', 3, CURRENT_TIMESTAMP + INTERVAL '15 minutes')
ON CONFLICT DO NOTHING;

-- 3. Owner Applications
INSERT INTO owner_applications (id, user_id, business_name, lot_type, status, submitted_at, reviewed_at, reviewed_by) VALUES
('11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'Owner A Corp', 'outdoor', 'approved', CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_TIMESTAMP, '00000000-0000-0000-0000-000000000000'),
('22222222-2222-2222-2222-222222222222', '22222222-2222-2222-2222-222222222222', 'Owner B LLC', 'basement', 'pending', CURRENT_TIMESTAMP, NULL, NULL)
ON CONFLICT DO NOTHING;

-- 4. Accounts and Grants (Note: tenant_id acts as logical boundary)
INSERT INTO accounts (id, user_id, tenant_id) VALUES
('11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111'), -- Owner A account
('44444444-4444-4444-4444-444444444444', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111') -- Operator for Owner A
ON CONFLICT DO NOTHING;

INSERT INTO operator_grants (account_id, created_by, permissions) VALUES
('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', ARRAY['DEVICE_STATUS_VIEW', 'CASH_COLLECT'])
ON CONFLICT DO NOTHING;

-- >>> PARKING SERVICE DB SEED <<<
-- 1. Tenants
INSERT INTO tenants (id, code, name, contact_email, contact_phone, status) VALUES
('11111111-1111-1111-1111-111111111111', 'T-OWNA', 'Owner A Corp', 'owner.a@test.com', '0911111111', 'ACTIVE')
ON CONFLICT DO NOTHING;

-- 2. Parking Sites (Floorless vs Multifloor)
INSERT INTO parking_sites (id, tenant_id, site_code, name, address, total_physical_capacity) VALUES
('10000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'S-FL', 'Floorless Lot', '123 Flat St', 100),
('10000000-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'S-MF', 'Multifloor Lot', '456 Deep St', 300)
ON CONFLICT DO NOTHING;

-- 3. Spatial Units (Hierarchy)
INSERT INTO spatial_units (id, tenant_id, site_id, parent_id, path, unit_type, name, max_capacity) VALUES
-- Floorless
('10000000-1000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000001', NULL, 'ZoneA', 'ZONE', 'Zone A', 50),
-- Multifloor
('10000000-2000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000002', NULL, 'B1', 'FLOOR', 'Basement 1', 150),
('10000000-2000-1000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000002', '10000000-2000-0000-0000-000000000001', 'B1/VIP', 'ZONE', 'VIP Zone', 20)
ON CONFLICT DO NOTHING;

-- >>> RESERVATION SERVICE DB SEED <<<
-- 1. Capacity Pools & Backup Scenarios
INSERT INTO site_capacity_pools (id, tenant_id, site_id, spatial_unit_id, total_capacity, max_reservation_quota, backup_count) VALUES
('10000000-3000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000001', '10000000-1000-0000-0000-000000000001', 50, 40, 5) -- 5 Backup slots for emergencies
ON CONFLICT DO NOTHING;
