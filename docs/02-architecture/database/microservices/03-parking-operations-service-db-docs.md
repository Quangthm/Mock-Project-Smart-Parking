# Parking Operations Service — Database Documentation

> **Service**: Parking Operations Service (`OperationsDb` — logical, triển khai trên 2 DB vật lý)  
> **Database files**: [`03a-parking-structure-ops-service-db.sql`](../../../../scripts/database/microservices/03a-parking-structure-ops-service-db.sql) → `smartpark_facility`; [`03b-reservation-operations-service-db.sql`](../../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) → `smartpark_operations`<br>
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý vòng đời vận hành động: đặt chỗ (`reservations`), phiên đỗ (`parking_sessions`), gán ô đỗ (`slot_allocations`), cấu hình hạn mức (`operational_capacity_configs`), chính sách thời gian (`operational_policies`), hạn ngạch backup (`structure_backup_policies`), thẻ/QR (`access_items`), vé tháng (`monthly_passes`), sự cố (`operational_incidents`, `operator_incident_events`), tìm kiếm (`search_facility_projections`), và audit (`audit_logs`).

---

## 0. Deployment profile

| Hạng mục | Giá trị |
|---|---|
| Database vật lý | `smartpark_facility` (03a, port `5442`) + `smartpark_operations` (03b, port `5443`) |
| Base DDL | `03a-parking-structure-ops-service-db.sql` + `03b-reservation-operations-service-db.sql` |
| Overlays | Parking: `parking-tenants`, `parking-structure-synchronizations`, `parking-operator-tables`, `parking-slot-constraints`; Reservation: `reservation-site-capacity-pools`, `reservation-structure-safety`, `reservation-structure-receipts`, `reservation-confirmation-time` |
| Deployment status | Tách 03 theo D1; 03a dùng `unit_id`/`count` (D4/P2); 03b khớp arch |
| Runtime deviation | Tên cột 03a khác arch; `site_capacity_pools` tạo lại bằng overlay (D7); `confirmed_at` do trigger stamp |

---

## 1. Tổng quan kiến trúc & Thuật toán Sức chứa v1.0

Parking Operations Service là **trái tim vận hành động** của SmartPark. Service logic là một bounded context, nhưng profile triển khai tách thành hai DB vật lý theo quyết định **D1**: 03a nằm trong `smartpark_facility`, 03b nằm trong `smartpark_operations`.

```text
Driver / Operator / IoT Entry → API Gateway
                                    ↓
        ┌───────────────────────────┴───────────────────────────┐
        ↓                                                       ↓
smartpark_facility (03a)                              smartpark_operations (03b)
┌──────────────────────────────┐                     ┌──────────────────────────────┐
│ structure_backup_policies    │                     │ operational_capacity_configs │
│ operator_incident_events     │                     │ operational_policies         │
└──────────────────────────────┘                     │ reservations                 │
                                                     │ parking_sessions             │
                                                     │ slot_allocations             │
                                                     │ access_items                 │
                                                     │ monthly_passes               │
                                                     │ operational_incidents        │
                                                     │ search_facility_projections  │
                                                     │ audit_logs                   │
                                                     └──────────────────────────────┘
```

### 1.1 Thuật toán Kiểm tra Sức chứa theo Khoảng Thời gian (Interval Overlap)

- **Vấn đề của bộ đếm phẳng (Scalar Counter Flaw):** dùng một số nguyên `reserved_count` để cộng dồn số lượng đặt chỗ là sai khi áp dụng cho các ngày khác nhau.
- **Giải pháp v1.0:** kiểm tra khả dụng dựa trên **khoảng thời gian chồng lấn** dùng `TSTZRANGE` và chỉ mục `GiST`.

### 1.2 Công thức Khả dụng theo Lý thuyết Tập hợp (Set Union Formula)

Theo SRS v1.0 RC3, tài nguyên không khả dụng tại thời điểm $t$ là **hợp (Union)** của các tập trạng thái, tránh trừ đúp:

$$\text{Unavailable}(t) = \text{Occupied}(t) \cup \text{Protected}(t) \cup \text{Pending\_Payment}(t) \cup \text{Backup}(t)$$

$$\text{Available}(t) = \text{Total} - |\text{Unavailable}(t)|$$

*Ví dụ:* bãi có 10 chỗ; 3 chỗ đang đỗ (`Occupied`), 2 chỗ giữ dự phòng (`Backup`), trong đó 1 chỗ dự phòng đang có xe thực tế. Khi đó $|\text{Unavailable}| = 4$ (không phải $3 + 2 = 5$); còn $10 - 4 = 6$ chỗ khả dụng.

### 1.3 Deployment topology (03a/03b)

| Mảng | DB vật lý | Bảng |
|---|---|---|
| Structure backup + operator incidents | `smartpark_facility` | `structure_backup_policies`, `operator_incident_events` |
| Reservations, sessions, allocations, access, passes, incidents, search, audit | `smartpark_operations` | 10 bảng còn lại |

Hai DB không có FK vật lý xuyên database; `tenant_id`, `site_id`, `slot_id`, `account_id` là Logical ID. Overlay của mỗi DB được mount riêng bởi `compose.schema-integration.yml`.

---

## 2. Bảng `operational_capacity_configs` — Định mức sức chứa vận hành

Lưu hạn mức nhận đặt trước tối đa (`max_reservation_quota`) và tổng sức chứa vật lý thiết kế cho từng khu vực/loại xe.

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh cấu hình |
| `tenant_id` | `UUID NOT NULL` | Logical Reference |
| `site_id` | `UUID NOT NULL` | Bãi xe áp dụng |
| `spatial_unit_id` | `UUID NULL` | Khu vực áp dụng (NULL = toàn bãi) |
| `vehicle_type` | `VARCHAR(50) NOT NULL CHECK (CAR/MOTORCYCLE/OVERSIZED)` | Loại xe |
| `total_physical_capacity` | `INT NOT NULL CHECK (>= 0)` | Sức chứa vật lý tối đa |
| `max_reservation_quota` | `INT NOT NULL CHECK (>= 0 AND <= total_physical_capacity)` | Hạn mức cho phép đặt trước |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` | Trạng thái hiệu lực |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật |

Unique: `(site_id, spatial_unit_id, vehicle_type)`; index `(site_id, vehicle_type)`.

---

## 3. Bảng `operational_policies` — Tham số thời gian vận hành

Quản lý thông số thời gian chuẩn hóa theo SRS v1.0:

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh chính sách |
| `tenant_id` | `UUID NOT NULL` | Logical Reference |
| `site_id` | `UUID NULL` | `NULL` = default toàn hệ thống |
| `arrival_late_tolerance_minutes` | `INT NOT NULL DEFAULT 15` | Ân hạn trễ giờ đến |
| `payment_hold_duration_minutes` | `INT NOT NULL DEFAULT 15` | Giữ chỗ chờ thanh toán |
| `min_reservation_duration_minutes` | `INT NOT NULL DEFAULT 15` | Thời lượng đặt tối thiểu |
| `max_reservation_duration_hours` | `INT NOT NULL DEFAULT 24` | Thời lượng đặt tối đa |
| `reservation_protection_window_hours` | `INT NOT NULL DEFAULT 4` | Cửa sổ bảo vệ chỗ đỗ |
| `allocation_lead_time_minutes` | `INT NOT NULL DEFAULT 60` | Tự động gán slot trước giờ đến |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` | Trạng thái hiệu lực |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật |

Unique `(tenant_id, site_id)`; index theo `site_id`.

---

## 4. Bảng `structure_backup_policies` — Hạn ngạch slot dự phòng

### 4.1 Mục đích nghiệp vụ

Quản lý số lượng chỗ đỗ cần bảo lưu làm dự phòng cho sự cố hoặc trường hợp khẩn cấp (`count`), tiếp nhận từ FacilityDb sang OperationsDb. Bảng nằm trong `smartpark_facility` (03a).

### 4.2 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh policy |
| `tenant_id` | `UUID NOT NULL` | Logical Reference tới UserDb |
| `site_id` | `UUID NOT NULL` | Logical Reference tới FacilityDb |
| `unit_id` | `UUID NULL` | Đơn vị không gian áp dụng; NULL = toàn site. Tên triển khai là `unit_id` (arch: `spatial_unit_id`, D4/P2) |
| `vehicle_type` | `VARCHAR(50) NOT NULL CHECK (CAR/MOTORCYCLE/OVERSIZED)` | Loại xe áp dụng |
| `count` | `INT NOT NULL CHECK (count >= 0)` | Số slot/capacity giữ làm backup. Tên triển khai là `count` (arch: `backup_count`, D4/P2) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật |

### 4.3 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_backup_site_policy` | `UNIQUE(site_id, vehicle_type) WHERE unit_id IS NULL` | Mỗi site chỉ có một policy backup cho một loại xe |
| `uq_backup_unit_policy` | `UNIQUE(site_id, unit_id, vehicle_type) WHERE unit_id IS NOT NULL` | Mỗi unit chỉ có một policy backup cho một loại xe |
| `(inline)` | `CHECK (vehicle_type IN ('CAR','MOTORCYCLE','OVERSIZED'))` | Loại xe hợp lệ (constraint không đặt tên trong DDL) |
| `(inline)` | `CHECK (count >= 0)` | Số lượng backup không âm (constraint không đặt tên trong DDL) |

---

## 5. Bảng `reservations` — Đặt chỗ trước

### 5.1 Mục đích nghiệp vụ

Quản lý yêu cầu đặt chỗ trước của tài xế. Ngăn đặt trùng xe qua GiST exclusion `no_overlapping_reservations_per_vehicle`. Dùng `version` cho Optimistic Concurrency Control.

### 5.2 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK` | Định danh đặt chỗ |
| `tenant_id`, `site_id`, `account_id` | `UUID NOT NULL` | Logical references |
| `target_spatial_unit_id`, `target_slot_id` | `UUID NULL` | Mục tiêu đặt chỗ; CHECK không đồng thời có cả hai |
| `original_plate`, `normalized_plate` | `VARCHAR(50)` | Biển số gốc và đã chuẩn hóa |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (CAR/MOTORCYCLE/OVERSIZED)` | Loại xe |
| `expected_start_time`, `expected_end_time` | `TIMESTAMPTZ NOT NULL` | Khung thời gian đặt chỗ; CHECK end > start |
| `confirmed_at` | `TIMESTAMPTZ NULL` | Thời điểm xác nhận; được stamp bởi trigger overlay `reservation_confirmation_time` |
| `status` | `VARCHAR(50) NOT NULL` | PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, EXPIRED, NO_SHOW, CANCELLED, UNFULFILLABLE |
| `hold_expires_at`, `cancelled_at` | `TIMESTAMPTZ NULL` | Hạn giữ chỗ / thời điểm hủy |
| `accepted_pricing_snapshot` | `JSONB NULL` | Snapshot giá đã chấp nhận |
| `version` | `INT NOT NULL DEFAULT 1` | Chống xung đột đồng thời (OCC) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo |

### 5.3 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `no_overlapping_reservations_per_vehicle` | `EXCLUDE USING gist (tenant_id WITH =, site_id WITH =, normalized_plate WITH =, tstzrange(expected_start_time, expected_end_time) WITH &&) WHERE status IN ('CONFIRMED','ALLOCATED','PARKING')` | Chống đặt trùng xe trong cùng khung giờ |
| `(inline)` | `CHECK (expected_end_time > expected_start_time)` | Khung giờ hợp lệ |
| `(inline)` | `CHECK (NOT (target_slot_id IS NOT NULL AND target_spatial_unit_id IS NOT NULL))` | Chỉ chọn slot hoặc unit, không đồng thời cả hai |
| `idx_reservations_tenant_site` | `(tenant_id, site_id)` | Truy vấn theo tenant/site |
| `idx_reservations_account` | `(account_id)` | Tra cứu theo tài khoản |
| `idx_reservations_status_times` | `(status, expected_start_time, expected_end_time)` | Lọc trạng thái/khung giờ |
| `idx_reservations_plate` | `(normalized_plate)` | Tra cứu biển số |

---

## 6. Bảng `parking_sessions` — Phiên đỗ xe thực tế

Lưu thông tin xe thực tế đỗ tại bãi: `entry_time`, `entry_image_url`, `exit_time`, `exit_image_url`, `access_item_id`, `ticket_reference`, `current_slot_id`, `normalized_plate`, và `session_status` (`ACTIVE`, `PENDING_PAYMENT`, `COMPLETED`, `OVERSTAYED`, `DISPUTED`). Unique index bảo đảm một ticket/access item chỉ gắn với một session `ACTIVE`.

---

## 7. Bảng `slot_allocations` — Gán tài nguyên ô đỗ chống Double-Booking

Đảm bảo một ô đỗ vật lý (`slot_id`) không bị gán trùng trong cùng khoảng thời gian qua `no_overlapping_slot_allocations` (`EXCLUDE USING gist`). Có `reservation_id`, `parking_session_id`, thời gian allocated và `allocation_status` (`RESERVED`, `OCCUPIED`, `RELEASED`). Gán theo spatial unit/pooled capacity không dùng bảng này; Reservation xử lý qua capacity ledger.

---

## 8. Bảng `access_items` — Thẻ từ & Mã QR ra vào

Quản lý vòng đời thẻ RFID/QR/plate/RFID: `identifier_code`, `item_type` (`CARD`, `QR`, `LICENSE_PLATE`, `RFID`), `status` (`AVAILABLE`, `IN_USE`, `LOST`, `DISABLED`), unique `(tenant_id, identifier_code)`.

---

## 9. Bảng `monthly_passes` — Vé tháng

Theo SRS v1.0, vé tháng chỉ áp dụng **WHEN_SPACE_AVAILABLE** (miễn trừ biểu phí khi bãi còn chỗ, không bảo đảm giữ slot cố định). `CHECK (entitlement_type = 'WHEN_SPACE_AVAILABLE')`; loại bỏ hoàn toàn `GUARANTEED_CAPACITY_SLOT`. Có GiST exclusion `no_overlapping_passes_per_vehicle`.

---

## 10. Bảng `operational_incidents` & `operator_incident_events`

### 10.1 `operational_incidents`

Quản lý sự cố ca trực: mất vé (`LOST_TICKET`), trạng thái slot không rõ (`UNKNOWN_SLOT_STATE`), lệch dữ liệu thanh toán lúc ra (`CHECKOUT_MISMATCH`), `OTHER`; trạng thái `OPEN`, `IN_PROGRESS`, `RESOLVED`, `CLOSED`; CHECK consistency khi resolve.

### 10.2 Bảng `operator_incident_events` (03a)

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa |
|---|---|---|
| `id` | `UUID PK` | Định danh sự kiện |
| `tenant_id`, `site_id`, `actor_id` | `UUID NOT NULL` | Logical IDs |
| `incident_type` | `VARCHAR(80) NOT NULL` | Loại sự kiện do Operator ghi nhận |
| `details` | `TEXT NOT NULL` | Mô tả chi tiết |
| `status` | `VARCHAR(20) NOT NULL DEFAULT 'reported' CHECK (reported/resolved)` | Trạng thái xử lý |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm ghi nhận |

Index: `(site_id, created_at DESC)`.

---

## 11. Bảng `search_facility_projections` — Module Tìm kiếm & Khám phá

Bảng chiếu dữ liệu phục vụ tìm kiếm bãi xe theo tọa độ GPS bán kính, trạng thái mở cửa, và số chỗ trống gần nhất mà không query xuyên database. Index `(latitude, longitude)` và `(operational_status, is_open)`.

---

## 12. Bảng `audit_logs` — Nhật ký kiểm toán cổng (Zero Cascade)

Ghi nhận thao tác mở barie, cưỡng chế gán slot, đổi trạng thái phiên đỗ của Operator. Append-only, không CASCADE. Index `(site_id, created_at DESC)` và `(actor_id)`.

---

## 13. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id` | Tất cả bảng | User & Access (`UserDb.tenants.id`) | Logical ID |
| `account_id` | `reservations`, `parking_sessions`, `monthly_passes` | User & Access (`UserDb.accounts.id`) | Logical ID |
| `site_id` | Tất cả bảng | Facility (`FacilityDb.parking_sites.id`) | Logical ID |
| `slot_id` | `slot_allocations` (`parking_sessions` dùng `current_slot_id`) | Facility (`FacilityDb.parking_slots.id`) | Logical ID |
| `unit_id` | `structure_backup_policies` | Facility (`FacilityDb.spatial_units.id`) | Logical ID; tên arch là `spatial_unit_id` |
| `reservation_id` | Payment Service | Operations (`reservations.id`) | Phục vụ thanh toán giữ chỗ |
| `parking_session_id` | Payment Service | Operations (`parking_sessions.id`) | Phục vụ thanh toán checkout |

03a (`smartpark_facility`) và 03b (`smartpark_operations`) là hai DB vật lý riêng; mọi tham chiếu giữa chúng và tới Facility/User đều là Logical ID, không có FK vật lý.

---

## 14. Deployment overlays

### 14.1 `smartpark_facility` (03a)

- `parking-tenants.sql` — `tenants` local copy (D5).
- `parking-structure-synchronizations.sql` — `structure_synchronizations` (D1/Q2).
- `parking-operator-tables.sql` — `support_tickets`, `operator_notifications`, `operator_audit_logs` (D3/D6).
- `parking-slot-constraints.sql` — `ck_deleted_slot_safe`, `uq_site_slot_code_ci` (D4).

`operator_incident_events` nằm trong base 03a, không lặp lại trong overlay.

### 14.2 `smartpark_operations` (03b)

- `reservation-site-capacity-pools.sql` — tạo `site_capacity_pools` đúng shape BE (D7); phải chạy trước safety overlay.
- `reservation-structure-safety.sql` — `structure_edit_holds`, `structure_site_state`, `structure_removed_resources` + guard trigger trên `reservations`, `parking_sessions`, `slot_allocations`, `site_capacity_pools`.
- `reservation-structure-receipts.sql` — `structure_release_receipts`.
- `reservation-confirmation-time.sql` — trigger stamp `confirmed_at`; không ADD COLUMN vì cột đã có trong base 03b.

---

## 15. Deviations so với arch `9a13499`

| Mã | Nội dung | Lý do |
|---|---|---|
| D1 | Tách `03` thành 03a (parking) + 03b (reservation) | 6-DB BE-first mapping |
| D4/P2 | 03a: `spatial_unit_id`→`unit_id`, `backup_count`→`count` | BE đang dùng tên cột này |
| D7 | `site_capacity_pools` tạo lại bằng overlay | Shape BE bị mất khi split arch |
| — | `confirmed_at` do trigger stamp | Base có cột nhưng không có writer |

Chi tiết quyết định: `docs/04-testing/reports/refactor-Docker Setup & Function Test/01-approved-decisions-D1-D7.md` và report 10.

---

## 16. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Hàm `uuid_generate_v4()` sinh khóa chính |
| `btree_gist` | Hỗ trợ `EXCLUDE USING gist` cho reservations/slot_allocations/monthly_passes |
