-- ==========================================
-- SPRINT 2 EDGE CASE TEST SCRIPT
-- ==========================================
-- Hướng dẫn: 
-- Chạy các lệnh dưới đây SAU KHI đã chạy file sprint2-seed.sql.
-- MỤC TIÊU LÀ TẤT CẢ CÁC LỆNH NÀY ĐỀU PHẢI BỊ DATABASE BÁO LỖI (THROW EXCEPTION).
-- Nếu lệnh nào chạy thành công (Success) -> Hệ thống có lỗ hổng bảo mật.

-- ---------------------------------------------------------
-- TEST 1: Unique Identity (Chống đăng ký trùng Email / SĐT)
-- Target DB: User Service DB
-- Expectation: Báo lỗi "uq_active_user_email_normalized"
-- ---------------------------------------------------------
INSERT INTO users (id, phone, email, password_hash, full_name, status) 
VALUES (uuid_generate_v4(), '0999999999', 'Owner.A@test.com', 'hash', 'Hacker', 'ACTIVE');
-- Note: Dù gõ chữ Hoa 'Owner.A' nhưng DB đã lưu email dạng lower() nên vẫn sẽ bị chặn.

-- ---------------------------------------------------------
-- TEST 2: Duplicate Normalized Contacts (Trùng lặp biển số xe)
-- Target DB: User Service DB
-- Expectation: Báo lỗi "uq_active_vehicle_plate"
-- ---------------------------------------------------------
-- Seed 1 biển số trước:
INSERT INTO vehicles (account_id, original_plate, normalized_plate, vehicle_type) 
VALUES ('11111111-1111-1111-1111-111111111111', '30A-123.45', '30A12345', 'CAR');

-- Cố tình thêm lại biển số với định dạng gốc khác, nhưng chuỗi normalize giống nhau:
INSERT INTO vehicles (account_id, original_plate, normalized_plate, vehicle_type) 
VALUES ('11111111-1111-1111-1111-111111111111', '30A 12345', '30A12345', 'CAR');

-- ---------------------------------------------------------
-- TEST 3: Ownership / Scope Boundary (Lỗ hổng phân quyền chéo bãi xe)
-- Target DB: Parking Service DB
-- Scenario: Owner B cố tình tạo một Khu vực (Zone) nhưng gán nó vào Bãi xe (Site) của Owner A.
-- Expectation: Báo lỗi Foreign Key "spatial_units_site_id_tenant_id_fkey"
-- ---------------------------------------------------------
INSERT INTO spatial_units (id, tenant_id, site_id, path, unit_type, name) 
VALUES (
    uuid_generate_v4(), 
    '22222222-2222-2222-2222-222222222222', -- Tenant của Owner B
    '10000000-0000-0000-0000-000000000001', -- Site 1 (Sở hữu bởi Owner A)
    'HackedZone', 
    'ZONE', 
    'Hacked Zone'
);

-- ---------------------------------------------------------
-- TEST 4: Cross-Lot Hierarchy (Lỗ hổng cấu trúc không gian)
-- Target DB: Parking Service DB
-- Scenario: Owner A tạo một Slot ở Bãi xe 1 (Site 1), nhưng gán ID Khu vực cha (parent_id) nằm ở Bãi xe 2 (Site 2).
-- Expectation: Báo lỗi Foreign Key "spatial_units_parent_id_tenant_id_site_id_fkey"
-- ---------------------------------------------------------
INSERT INTO spatial_units (id, tenant_id, site_id, parent_id, path, unit_type, name) 
VALUES (
    uuid_generate_v4(), 
    '11111111-1111-1111-1111-111111111111', -- Tenant Owner A
    '10000000-0000-0000-0000-000000000001', -- Đang ở Site 1
    '10000000-2000-1000-0000-000000000001', -- Gán Parent ID là Khu VIP nằm ở Site 2
    'Site1_Zone/Hacked', 
    'ZONE', 
    'Hacked Zone 2'
);

-- ---------------------------------------------------------
-- TEST 5: Invalid Capacity / Overbooking (Lỗ hổng vượt quá sức chứa)
-- Target DB: Reservation Service DB
-- Scenario: Hệ thống (hoặc một lỗi đồng thời) cố gắng tăng số lượng xe đang bảo lưu (Reserved) vượt mức Quota cho phép.
-- Expectation: Báo lỗi Check Constraint "site_capacity_pools_check"
-- ---------------------------------------------------------
-- Ở file seed, Pool '10000000-3000...' có max_reservation_quota = 40.
-- Thử nâng current_reserved_count lên 45:
UPDATE site_capacity_pools 
SET current_reserved_count = 45 
WHERE id = '10000000-3000-0000-0000-000000000001';

-- ---------------------------------------------------------
-- TEST 6: Invalid Enum/Status (Hack trạng thái hệ thống)
-- Target DB: User Service DB
-- Scenario: Thử tạo user với trạng thái không có trong danh sách cho phép (VD: 'SUPER_ADMIN')
-- Expectation: Báo lỗi Check Constraint "users_status_check"
-- ---------------------------------------------------------
INSERT INTO users (id, phone, email, password_hash, full_name, status) 
VALUES (uuid_generate_v4(), '0988888888', 'hacker2@test.com', 'hash', 'Hacker 2', 'SUPER_ADMIN');

-- ---------------------------------------------------------
-- TEST 7: Operator Permissions Boundary (Ngăn chặn leo thang đặc quyền)
-- Target DB: User Service DB
-- Scenario: Cố tình cấp quyền 'DELETE_ALL_DATA' không tồn tại trong danh sách cho phép
-- Expectation: Báo lỗi Check Constraint "operator_grants_check"
-- ---------------------------------------------------------
INSERT INTO operator_grants (account_id, created_by, permissions) 
VALUES (
    '11111111-1111-1111-1111-111111111111', 
    '00000000-0000-0000-0000-000000000000', 
    ARRAY['DEVICE_MANAGE', 'DELETE_ALL_DATA']
);

-- ---------------------------------------------------------
-- TEST 8: Time Logic Violation (Lỗi logic thời gian đặt chỗ)
-- Target DB: Reservation Service DB
-- Scenario: Đặt thời gian kết thúc (end_time) TRƯỚC thời gian bắt đầu (start_time)
-- Expectation: Báo lỗi Check Constraint "reservations_check"
-- ---------------------------------------------------------
INSERT INTO reservations (
    tenant_id, site_id, account_id, expected_start_time, expected_end_time, status
) VALUES (
    '11111111-1111-1111-1111-111111111111', 
    '10000000-0000-0000-0000-000000000001', 
    '33333333-3333-3333-3333-333333333333', 
    CURRENT_TIMESTAMP + INTERVAL '2 hours', 
    CURRENT_TIMESTAMP + INTERVAL '1 hour',  
    'PENDING_PAYMENT'
);

-- ---------------------------------------------------------
-- TEST 9: Financial Integrity (Gian lận số tiền thanh toán)
-- Target DB: Payment Service DB
-- Scenario: Tổng tiền (final_amount) không khớp với công thức
-- Expectation: Báo lỗi Check Constraint "payment_orders_check"
-- ---------------------------------------------------------
INSERT INTO payment_orders (
    tenant_id, site_id, order_code, order_type, reference_type, reference_id, 
    amount, discount_amount, deposit_deducted, final_amount
) VALUES (
    '11111111-1111-1111-1111-111111111111', 
    '10000000-0000-0000-0000-000000000001', 
    'ORDER-HACK-001', 
    'PARKING_FEE', 
    'RESERVATION', 
    uuid_generate_v4(), 
    100000, 
    20000,  
    0,      
    50000   -- Sai, đáng lẽ phải là 80000
);

-- ---------------------------------------------------------
-- TEST 10: Double Booking / Overlapping Slots (Chống cấp phát trùng chỗ)
-- Target DB: Reservation Service DB
-- Scenario: Cố tình cấp phát 2 lần chồng lấp thời gian trên cùng 1 slot
-- Expectation: Báo lỗi Exclusion Constraint "no_overlapping_slot_allocations"
-- ---------------------------------------------------------
INSERT INTO slot_allocations (tenant_id, site_id, slot_id, allocated_start_time, allocated_end_time, allocation_status) 
VALUES (
    '11111111-1111-1111-1111-111111111111', 
    '10000000-0000-0000-0000-000000000001', 
    '10000000-4000-0000-0000-000000000001', 
    '2026-10-10 08:00:00+00', 
    '2026-10-10 10:00:00+00', 
    'RESERVED'
);

INSERT INTO slot_allocations (tenant_id, site_id, slot_id, allocated_start_time, allocated_end_time, allocation_status) 
VALUES (
    '11111111-1111-1111-1111-111111111111', 
    '10000000-0000-0000-0000-000000000001', 
    '10000000-4000-0000-0000-000000000001', 
    '2026-10-10 09:00:00+00', 
    '2026-10-10 11:00:00+00', 
    'RESERVED'
);
