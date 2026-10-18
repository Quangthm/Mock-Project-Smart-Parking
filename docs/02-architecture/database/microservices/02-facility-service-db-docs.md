# Facility & Visualization Service — Database Documentation

> **Service**: Facility & Visualization Service (`FacilityDb`)  
> **Database file**: [`02-facility-service-db.sql`](../../../../scripts/database/microservices/02-facility-service-db.sql)  
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý toàn bộ tài nguyên hạ tầng vật lý tĩnh của bãi đỗ xe: bãi đỗ (`parking_sites`), phân cấp không gian (`spatial_units`), sức chứa cấu hình tĩnh (`spatial_unit_capacities`), ô đỗ vật lý và tọa độ 3D (`parking_slots`), tương thích loại xe (`slot_compatibilities`), luồng di chuyển nội bộ (`parking_access_paths`), thao tác cấu trúc (`structure_operations`), và lịch sử kiểm toán thay đổi cấu trúc (`structure_slot_audit`, `structure_change_audit`).

---

## 1. Tổng quan kiến trúc

Facility & Visualization Service là **dịch vụ quản lý tài nguyên và cấu trúc vật lý tĩnh** của hệ sinh thái SmartPark theo chuẩn kiến trúc v1.0 RC3. Service này chỉ chịu trách nhiệm định nghĩa tài nguyên vật lý, hình học không gian 3D, và sức chứa thiết kế tĩnh. Toàn bộ logic động về vận hành (phiên đỗ, đặt chỗ, tính tiền, thẻ từ, sự cố) thuộc quyền quản lý của các service tương ứng (Operations, Payment).

```text
Owner/Admin → API Gateway
                   ↓
   Facility & Visualization DB (FacilityDb)
   ┌────────────────────────────────────────┐
   │ parking_sites                          │ ← Bãi đỗ xe vật lý
   │ spatial_units                          │ ← Zone / Floor / Block phân cấp
   │ spatial_unit_capacities (Static)       │ ← Sức chứa thiết kế tĩnh
   │ parking_slots                          │ ← Ô đỗ vật lý (tọa độ 3D)
   │ slot_compatibilities                   │ ← Tương thích loại xe
   │ parking_access_paths                   │ ← Luồng di chuyển trong bãi
   │ structure_operations                   │ ← Thao tác điều chỉnh cấu trúc
   │ structure_slot_audit (Zero Cascade)    │ ← Lịch sử thay đổi slot
   │ structure_change_audit (Zero Cascade)  │ ← Lịch sử snapshot cấu trúc
   └────────────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi v1.0:**
1. **Ranh giới vật lý thuần túy (Pure Physical Resources):** Facility chỉ quản lý những gì tồn tại vật lý. Bảng `spatial_unit_capacities` chỉ lưu `max_capacity` tĩnh; cột `current_occupied` và `reserved_count` đã được loại bỏ để chuyển về quyền quản lý của Parking Operations Service.
2. **Loại bỏ trạng thái đặt chỗ khỏi ô đỗ:** `parking_slots` chỉ lưu `physical_state` (AVAILABLE, OCCUPIED, UNKNOWN, MAINTENANCE, UNAVAILABLE); cột `reservation_state` thuộc về Operations Service.
3. **Bảo tồn lịch sử kiểm toán tuyệt đối (Zero Cascade on Audit):** Bỏ toàn bộ `ON DELETE CASCADE` trên các bảng audit (`structure_slot_audit`, `structure_change_audit`), sử dụng `RESTRICT` hoặc `SET NULL` để đảm bảo log kiểm toán không bao giờ bị biến mất.
4. **Đính chính kỹ thuật Composite FK vs RLS:** Composite Foreign Keys chỉ đảm bảo tính toàn vẹn dữ liệu cha-con, **không** thay thế cho Row-Level Security (RLS) của PostgreSQL. RLS phải được kích hoạt độc lập khi cần cô lập dữ liệu người dùng.
5. **Giao tiếp bất đồng bộ bền vững:** Sử dụng **RabbitMQ + MassTransit** để công bố các sự kiện thay đổi cấu trúc (`ChangeProposed`, `ChangeCommitted`, `EmergencyRestrictionRecorded`).

---

## 2. Bảng `parking_sites` — Bãi đỗ xe vật lý

### 2.1 Mục đích nghiệp vụ

Lưu trữ thông tin cơ sở hạ tầng bãi đỗ xe vật lý. Mỗi bãi xe thuộc một tổ chức (`tenant_id` - Logical Reference từ UserDb) và có mã bãi (`site_code`), tên, địa chỉ, tọa độ GPS, tổng sức chứa thiết kế, và trạng thái phê duyệt/vận hành.

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-FAC-01** | C | Quản lý thông tin bãi đỗ xe vật lý |
| **FR-AUTH-05** | C | Bãi xe khởi tạo ở trạng thái PENDING_APPROVAL từ đơn đăng ký Owner |
| **BR-FAC-01** | — | Trạng thái bãi xe: PENDING_APPROVAL, ACTIVE, INACTIVE, MAINTENANCE, SUSPENDED |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của bãi xe | Tự sinh bởi database | FR-FAC-01 |
| `tenant_id` | `UUID NOT NULL` | Logical Reference trỏ tới `tenants.id` trong UserDb | Lấy từ ngữ cảnh Owner duyệt đơn | FR-AUTH-05 |
| `site_code` | `VARCHAR(50) NOT NULL` | Mã viết tắt của bãi xe (duy nhất trong tenant) | Do Owner nhập | FR-FAC-01 |
| `name` | `VARCHAR(255) NOT NULL` | Tên thương mại bãi đỗ xe | Do Owner nhập | FR-FAC-01 |
| `address` | `TEXT NOT NULL` | Địa chỉ thực tế của bãi xe | Do Owner nhập | FR-FAC-01 |
| `latitude` | `DECIMAL(10,8) NULL` | Tọa độ vĩ độ GPS phục vụ bản đồ | Do Owner nhập / geocoding | FR-FAC-01 |
| `longitude` | `DECIMAL(11,8) NULL` | Tọa độ kinh độ GPS phục vụ bản đồ | Do Owner nhập / geocoding | FR-FAC-01 |
| `total_physical_capacity` | `INT NOT NULL DEFAULT 0` | Tổng sức chứa thiết kế của bãi (>= 0) | Tính tổng cấu hình hoặc Owner khai | FR-FAC-01 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái bãi: PENDING_APPROVAL, ACTIVE, INACTIVE, MAINTENANCE, SUSPENDED | Admin/Owner quản lý | BR-FAC-01 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bãi | Database tự sinh | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật cuối | Application cập nhật | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete timestamp | Application cập nhật | — |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `chk_sites_status` | `CHECK (status IN ('PENDING_APPROVAL', 'ACTIVE', 'INACTIVE', 'MAINTENANCE', 'SUSPENDED'))` | Giới hạn trạng thái bãi hợp lệ |
| `chk_sites_capacity` | `CHECK (total_physical_capacity >= 0)` | Sức chứa không âm |
| `uq_active_site_code_ci` | `UNIQUE(tenant_id, upper(site_code)) WHERE deleted_at IS NULL` | Đảm bảo mã bãi duy nhất trong cùng tenant |
| `idx_parking_sites_tenant_id` | `tenant_id` | Lọc danh sách bãi theo tenant |
| `idx_parking_sites_status` | `status` | Lọc bãi theo trạng thái hoạt động |

---

## 3. Bảng `spatial_units` — Đơn vị không gian phân cấp (Zone / Floor / Block)

### 3.1 Mục đích nghiệp vụ

Phân chia không gian vật lý trong bãi đỗ xe thành các đơn vị phân cấp hình cây (Tree hierarchy): Tầng (`FLOOR`), Khu vực (`ZONE`), hoặc Dãy (`BLOCK`).

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-FAC-02** | C | Phân chia đơn vị không gian theo sơ đồ bãi đỗ |
| **BR-FAC-02** | — | Hierarchy: Block -> Floor -> Zone không chứa chu trình |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của đơn vị không gian | Tự sinh bởi database | FR-FAC-02 |
| `tenant_id` | `UUID NOT NULL` | Logical Reference trỏ tới `tenants.id` | Kế thừa từ site | FR-FAC-02 |
| `site_id` | `UUID NOT NULL` | FK tham chiếu tới `parking_sites.id` | Thuộc về bãi xe nào | FR-FAC-02 |
| `parent_id` | `UUID NULL` | FK đệ quy tham chiếu tới đơn vị không gian cha | NULL nếu là tầng/đơn vị gốc | FR-FAC-02 |
| `path` | `TEXT NOT NULL` | Materialized path phân cấp (ví dụ: `/B1/ZONE_A`) | Application tính toán | FR-FAC-02 |
| `unit_type` | `VARCHAR(50) NOT NULL` | Loại đơn vị: `'ZONE'`, `'FLOOR'`, `'BLOCK'` | Do Owner cấu hình | FR-FAC-02 |
| `name` | `VARCHAR(100) NOT NULL` | Tên hiển thị (Tầng hầm 1, Khu A...) | Do Owner nhập | FR-FAC-02 |
| `max_capacity` | `INT NOT NULL DEFAULT 0` | Sức chứa tối đa của đơn vị (>= 0) | Do Owner cấu hình | FR-FAC-02 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi | Database tự sinh | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật cuối | Application cập nhật | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete timestamp | Application cập nhật | — |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_spatial_units_site` | `FOREIGN KEY (site_id) REFERENCES parking_sites(id) ON DELETE CASCADE` | Xóa bãi xe thì cascade xóa đơn vị không gian |
| `FK_spatial_units_parent` | `FOREIGN KEY (parent_id) REFERENCES spatial_units(id) ON DELETE CASCADE` | Xóa đơn vị cha thì cascade xóa đơn vị con |
| `uq_active_unit_name_ci` | `UNIQUE(site_id, parent_id, lower(name)) WHERE deleted_at IS NULL` | Tên đơn vị duy nhất trong cùng cấp cha |
| `idx_spatial_units_site_id` | `site_id` | Truy vấn các đơn vị trong cùng bãi xe |

---

## 4. Bảng `spatial_unit_capacities` — Sức chứa cấu hình tĩnh theo loại xe

### 4.1 Mục đích nghiệp vụ

Lưu trữ **sức chứa thiết kế tĩnh (Static Capacity Configuration)** của từng đơn vị không gian theo loại xe (CAR, MOTORCYCLE, OVERSIZED). Bảng này giải quyết bài toán:
- Quản lý bãi xe máy hoặc khu vực đỗ linh hoạt không đánh số ô riêng lẻ.
- **Tuân thủ Bounded Context v1.0:** Chỉ lưu `max_capacity` thiết kế. Không lưu `current_occupied` hay `reserved_count` (Operations Service tự quản lý dữ liệu động này).

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-FAC-03** | C | Khai báo định mức sức chứa phân loại xe cho Zone/Floor |
| **BR-CAP-01** | — | Facility sở hữu configured capacity; Operations sở hữu occupancy |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi sức chứa | Tự sinh bởi database | FR-FAC-03 |
| `tenant_id` | `UUID NOT NULL` | Logical Reference tenant | Kế thừa từ site | — |
| `site_id` | `UUID NOT NULL` | FK tham chiếu tới `parking_sites.id` | Thuộc về bãi xe nào | FR-FAC-03 |
| `spatial_unit_id` | `UUID NOT NULL` | FK tham chiếu tới `spatial_units.id` | Thuộc đơn vị không gian nào | FR-FAC-03 |
| `vehicle_type` | `VARCHAR(50) NOT NULL` | Loại xe: `'CAR'`, `'MOTORCYCLE'`, `'OVERSIZED'` | Cấu hình cho loại xe nào | FR-FAC-03 |
| `max_capacity` | `INT NOT NULL CHECK (max_capacity >= 0)` | Sức chứa thiết kế tối đa | Do Owner thiết lập | BR-CAP-01 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo cấu hình | Database tự sinh | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật cuối | Application cập nhật | — |

### 4.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_suc_spatial_unit` | `FOREIGN KEY (spatial_unit_id) REFERENCES spatial_units(id) ON DELETE CASCADE` | Xóa đơn vị không gian thì xóa định mức sức chứa |
| `uq_spatial_unit_vehicle_type` | `UNIQUE(spatial_unit_id, vehicle_type)` | Mỗi đơn vị không gian chỉ có 1 định mức cho 1 loại xe |
| `idx_spatial_unit_capacities_site` | `site_id, spatial_unit_id, vehicle_type` | Tối ưu tra cứu định mức sức chứa bãi |

---

## 5. Bảng `parking_slots` — Chỗ đỗ vật lý & Tọa độ 3D

### 5.1 Mục đích nghiệp vụ

Lưu trữ thông tin chi tiết từng ô đỗ xe vật lý đánh số riêng lẻ: mã ô (`slot_code`), loại ô (`STANDARD`, `EV`, `DISABLED`, `VIP`), tọa độ không gian 3D (`coordinates_3d`) phục vụ Digital Twin, và trạng thái vật lý thực tế (`physical_state`).

### 5.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-FAC-04** | C | Khởi tạo ô đỗ, gán mã ô và tọa độ 3D |
| **BR-FAC-03** | — | Phân loại ô đỗ: STANDARD, EV, DISABLED, VIP |
| **BR-FAC-04** | — | Trạng thái vật lý: AVAILABLE, OCCUPIED, UNKNOWN, MAINTENANCE, UNAVAILABLE |

### 5.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất ô đỗ | Tự sinh bởi database | FR-FAC-04 |
| `tenant_id` | `UUID NOT NULL` | Logical Reference tenant | Kế thừa từ site | — |
| `site_id` | `UUID NOT NULL` | FK tham chiếu tới `parking_sites.id` | Thuộc bãi xe nào | FR-FAC-04 |
| `spatial_unit_id` | `UUID NOT NULL` | FK tham chiếu tới `spatial_units.id` | Thuộc khu vực/tầng nào | FR-FAC-04 |
| `slot_code` | `VARCHAR(50) NOT NULL` | Mã định danh ô đỗ (A-01, B-12...) | Do Owner nhập | FR-FAC-04 |
| `slot_type` | `VARCHAR(50) NOT NULL DEFAULT 'STANDARD'` | Loại ô đỗ: `'STANDARD'`, `'EV'`, `'DISABLED'`, `'VIP'` | Cấu hình ô đỗ | BR-FAC-03 |
| `features` | `JSONB NULL` | Thuộc tính mở rộng (có mái che, sạc nhanh...) | Cấu hình linh hoạt | FR-FAC-04 |
| `coordinates_3d` | `JSONB NULL` | Tọa độ x, y, z và góc xoay 3D | Mesh mapping trong 3D Digital Twin | FR-FAC-04 |
| `physical_state` | `VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE'` | Trạng thái cảm biến/vật lý: AVAILABLE, OCCUPIED, UNKNOWN, MAINTENANCE, UNAVAILABLE | Cảm biến IoT / Barrier / Operator cập nhật | BR-FAC-04 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo | Database tự sinh | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật cuối | Application cập nhật | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete timestamp | Application cập nhật | — |

### 5.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `chk_slot_type` | `CHECK (slot_type IN ('STANDARD', 'EV', 'DISABLED', 'VIP'))` | Hỗ trợ đầy đủ 4 loại ô đỗ chuẩn v1.0 |
| `chk_slot_physical_state` | `CHECK (physical_state IN ('AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE'))` | Trạng thái vật lý hợp lệ |
| `uq_active_slot_code_ci` | `UNIQUE(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL` | Mã ô duy nhất trong đơn vị không gian |
| `idx_parking_slots_spatial_unit_id` | `spatial_unit_id` | Truy vấn các ô đỗ trong khu vực |
| `idx_parking_slots_physical_state` | `physical_state` | Lọc nhanh ô đỗ trống/đang đỗ |

---

## 6. Bảng `slot_compatibilities` — Tương thích loại xe cho ô đỗ

### 6.1 Mục đích nghiệp vụ

Định nghĩa loại phương tiện được phép đỗ vào từng ô đỗ (`CAR`, `'MOTORCYCLE'`, `'OVERSIZED'`).

### 6.2 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh |
|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi | Tự sinh |
| `tenant_id` | `UUID NOT NULL` | Logical Reference | Kế thừa từ slot |
| `site_id` | `UUID NOT NULL` | Logical Reference | Kế thừa từ slot |
| `slot_id` | `UUID NOT NULL REFERENCES parking_slots(id) ON DELETE CASCADE` | Ô đỗ cần khai báo tương thích | Thuộc ô đỗ nào |
| `vehicle_type` | `VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED'))` | Loại xe tương thích | Cấu hình ô |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo | Database tự sinh |

---

## 7. Bảng `parking_access_paths` — Luồng di chuyển nội bộ

### 7.1 Mục đích nghiệp vụ

Định nghĩa đường đi, lối vào/ra giữa các đơn vị không gian phục vụ chỉ dẫn đường đi cho tài xế và mô phỏng xe chạy trong Digital Twin 3D.

### 7.2 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh luồng đi |
| `tenant_id` | `UUID NOT NULL` | Logical Reference |
| `site_id` | `UUID NOT NULL REFERENCES parking_sites(id) ON DELETE RESTRICT` | Thuộc bãi xe nào |
| `path_code` | `VARCHAR(50) NOT NULL` | Mã đường đi nội bộ |
| `from_unit_id` | `UUID REFERENCES spatial_units(id) ON DELETE RESTRICT` | Điểm bắt đầu (đơn vị không gian) |
| `to_unit_id` | `UUID REFERENCES spatial_units(id) ON DELETE RESTRICT` | Điểm đến (đơn vị không gian) |
| `map_data` | `JSONB NULL` | Dữ liệu waypoint/vector tọa độ đường đi |
| `created_at` / `deleted_at` | `TIMESTAMPTZ` | Thời điểm tạo / xóa logic |

---

## 8. Bảng Thao tác & Kiểm toán Cấu trúc (Zero Cascade)

### 8.1 Bảng `structure_operations`

Lưu trữ các phiên thao tác điều chỉnh cấu trúc bãi xe (thay đổi layout, phân lại ô, chia khu vực) có tính chất idempotent để tránh áp dụng trùng lặp.

### 8.2 Bảng `structure_slot_audit`

Lưu lịch sử tác động lên từng ô đỗ. **Nguyên tắc bảo toàn Audit:** Sử dụng `ON DELETE RESTRICT` trên `site_id` và `ON DELETE SET NULL` trên `slot_id` — tuyệt đối cấm CASCADE làm mất lịch sử audit.

### 8.3 Bảng `structure_change_audit`

Lưu snapshot trước (`before_state`) và sau (`after_state`) của cấu trúc bãi xe khi có chỉnh sửa. Có index `ix_structure_change_history` trên `(site_id, created_at)` phục vụ tra cứu lịch sử thay đổi theo thời gian.

---

## 9. Tóm tắt quan hệ giữa các bảng

```text
parking_sites (1) ──── (0..N) spatial_units
parking_sites (1) ──── (0..N) spatial_unit_capacities
parking_sites (1) ──── (0..N) parking_slots
parking_sites (1) ──── (0..N) parking_access_paths
parking_sites (1) ──── (0..N) structure_operations
parking_sites (1) ──── (0..N) structure_slot_audit
parking_sites (1) ──── (0..N) structure_change_audit

spatial_units (1) ──── (0..N) spatial_units [parent_id đệ quy]
spatial_units (1) ──── (0..N) spatial_unit_capacities [spatial_unit_id]
spatial_units (1) ──── (0..N) parking_slots [spatial_unit_id]
spatial_units (1) ──── (0..N) parking_access_paths [from_unit_id, to_unit_id]

parking_slots (1) ──── (0..N) slot_compatibilities [slot_id]
parking_slots (1) ──── (0..N) structure_slot_audit [slot_id ON DELETE SET NULL]

structure_operations (1) ──── (0..N) structure_change_audit [operation_id ON DELETE SET NULL]
```

---

## 10. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id` | Tất cả bảng | User & Access Service (`UserDb.tenants.id`) | Logical ID; xác thực tồn tại qua API trước khi tạo bãi; nhận RabbitMQ event `TenantDeleted` |
| `actor_id` | `structure_operations`, `structure_slot_audit`, `structure_change_audit` | User & Access Service (`UserDb.users.id`) | Logical ID người thực hiện thao tác cấu trúc |
| `site_id` | Các service khác | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID bãi xe dùng trong Operations, Payment, IoT |
| `slot_id` | OperationsDb | Facility & Visualization (`FacilityDb.parking_slots.id`) | Logical ID gán slot trong đặt chỗ và phiên đỗ |

---

## 11. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Hàm `uuid_generate_v4()` sinh khóa chính ngẫu nhiên duy nhất |
