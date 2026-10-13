# Reservation & Session Service — Database Documentation

> **Service**: Reservation & Session Service  
> **Database file**: [`03-reservation-session-service-db.sql`](../../../../scripts/database/microservices/03-reservation-session-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý hạn ngạch sức chứa (capacity pools), đặt chỗ trước (reservations), phiên đỗ xe thực tế (parking sessions), việc cấp phát vị trí đỗ vật lý (slot allocations), và vé tháng dài hạn (monthly passes).

---

## 1. Tổng quan kiến trúc

Reservation & Session Service là core engine xử lý các giao dịch giữ chỗ và đỗ xe thực tế. Dữ liệu vật lý (từ Parking Service) và dữ liệu con người (từ User Service) được liên kết vào đây qua các **Logical IDs**.

```text
User/Parking Service (Events) → API Gateway
                                    ↓
                 Reservation & Session Service DB
                 ┌─────────────────────────────────┐
                 │ site_capacity_pools             │ ← Hạn mức đặt chỗ
                 │ reservations                    │ ← Booking trước khi đến
                 │ monthly_passes                  │ ← [Merged] Vé tháng
                 │ parking_sessions                │ ← Phiên đỗ xe thực tế
                 │ slot_allocations                │ ← Gán slot vật lý
                 └─────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- **Không có FK vật lý xuyên Service**: Mọi liên kết ra ngoài (`tenant_id`, `site_id`, `spatial_unit_id`, `slot_id`, `account_id`) đều là Logical ID. `(tenant_id, site_id)` được duy trì ở mọi bảng làm composite context.
- **Tách bạch trạng thái Booking và Physical**: Một `reservation` có trạng thái business riêng rẽ với `parking_session` và với `physical_state` bên Parking Service (§3.2.2).
- **Chống trùng lặp bằng PostgreSQL GiST**: Dùng `EXCLUDE USING gist` để ngăn hai booking cùng khung giờ cho một xe, hoặc hai xe cùng tranh một slot.

---

## 2. Bảng `site_capacity_pools` — Quản lý hạn ngạch sức chứa

### 2.1 Mục đích nghiệp vụ

Thực thi nguyên tắc **Continuous Capacity and Availability Tracking** (§3.4.5). Đây là bảng counter tổng hợp cho toàn site hoặc từng zone, phân loại theo `vehicle_type`. Khi tài xế tìm kiếm bãi hoặc chuẩn bị đặt chỗ, hệ thống tính toán sức chứa còn lại dựa trên công thức:
`Available = Total − Occupied − Protected − Pending Payment − Backup`

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-CAP-02** | C | Display capacity dimensions separately theo vehicle category; tính toán availability; pending payment consumes capacity |
| **FR-CAP-03** | C | Enforce canonical capacity/availability invariants (§3.4.3–6) |
| **FR-RES-01** | R/A | Create reservation; kiểm tra capacity |
| **§3.4.5** | — | Công thức Available Capacity |
| **US-D03** | US | Tài xế xem số chỗ trống khi tìm bãi đỗ |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh pool sức chứa | Tự sinh bởi database | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Parking Service context | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ xe | Parking Service context | FR-CAP-02 |
| `spatial_unit_id` | `UUID NULL` | Logical ID của Zone. Nếu `NULL`, pool này là tổng cho toàn site. Nếu có giá trị, đây là pool cho một zone cụ thể (phục vụ Zone Reservation) | Cập nhật từ cấu trúc Parking Service qua event | FR-RES-03 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại xe của pool này. Sức chứa ô tô và xe máy được quản lý hoàn toàn độc lập | Owner cấu hình | FR-CAP-02 |
| `total_capacity` | `INT NOT NULL` | Tổng số slot vật lý (Total Capacity) | Đồng bộ từ Parking Service | §3.4.5 |
| `max_reservation_quota` | `INT NOT NULL` | Số lượng tối đa được phép nhận booking trước (để chừa chỗ cho walk-in) | Owner cấu hình | §3.2.1 |
| `current_reserved_count` | `INT NOT NULL DEFAULT 0` | Số reservation đang ở trạng thái `CONFIRMED` nhưng chưa đến giờ bảo vệ (Protected) | Tăng giảm khi reservation status thay đổi | §3.4.5 |
| `pending_payment_count` | `INT NOT NULL DEFAULT 0` | Số reservation đang ở `PENDING_PAYMENT` (Hold duration ~5 phút). Trừ vào Available Capacity | Tăng giảm trong window thanh toán | §3.4.3; §3.4.5 |
| `occupied_count` | `INT NOT NULL DEFAULT 0` | Số xe đang thực sự đỗ trong bãi thuộc nhóm xe này | Tính từ các active `parking_sessions` | §3.4.5 |
| `protected_count` | `INT NOT NULL DEFAULT 0` | Số lượng capacity đang nằm trong Reservation Protection Window (VD: 4h trước giờ đến) để chặn walk-in | Application update bằng background job/trigger | §3.4.4; §3.4.5 |
| `backup_count` | `INT NOT NULL DEFAULT 0` | Số chỗ dự phòng không cho booking/walk-in bình thường, dùng để giải quyết conflict | Owner cấu hình | §3.4.5 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật count gần nhất | Application gán | — |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_site_capacity_pools` | `UNIQUE (id, tenant_id, site_id)` | Hỗ trợ composite keys, đảm bảo tính nhất quán |
| `chk_vehicle_type` | `CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE'))` | Ràng buộc loại xe hợp lệ |
| `chk_reserved_count` | `CHECK (current_reserved_count >= 0 AND current_reserved_count <= max_reservation_quota)` | Đảm bảo số lượng giữ chỗ không được âm và không vượt qua quota |
| `chk_max_quota` | `CHECK (max_reservation_quota <= total_capacity)` | Quota giữ chỗ không được lớn hơn tổng số chỗ vật lý của bãi |
| `idx_site_capacity_pools_tenant_site` | `(tenant_id, site_id)` | Query nhanh để tính tổng capacity cho một bãi |

---

## 3. Bảng `reservations` — Đặt chỗ trước

### 3.1 Mục đích nghiệp vụ

Quản lý **vòng đời đặt chỗ trước** của người dùng. Hỗ trợ 3 chế độ (FR-BAS-01): Specific Slot, Zone, Capacity. Reservation tách rời hoàn toàn với phiên đỗ xe vật lý.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-RES-03** | C | Áp dụng Specific Slot, Zone, Capacity modes; Allocation Lead Time |
| **FR-RES-12** | A/X | Validate lifecycle transitions (PENDING_PAYMENT → CONFIRMED → ALLOCATED) |
| **FR-RES-13** | C | Allocation ranking dựa trên start time, confirmation time |
| **FR-CAP-04** | A | Ngăn chặn duplicate reservations (chống 1 xe book 2 bãi cùng lúc) |
| **§3.4.2** | — | Vòng đời reservation (PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, EXPIRED, NO_SHOW, CANCELLED, UNFULFILLABLE) |
| **US-D01** | US | Tài xế đặt chỗ trước |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh đặt chỗ | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ | Context | §3.2.1 |
| `account_id` | `UUID NOT NULL` | Logical ID người đặt (trỏ sang User Service) | Request context | FR-RES-01 |
| `target_spatial_unit_id` | `UUID NULL` | Logical ID của Zone nếu chọn Zone Reservation Mode. `NULL` nếu book theo Capacity | Driver chọn (Zone mode) | FR-BAS-01; §3.4.1 |
| `target_slot_id` | `UUID NULL` | Logical ID của Slot cụ thể nếu chọn Specific Slot Reservation Mode. `NULL` nếu book theo Zone/Capacity | Driver chọn (Slot mode) | FR-BAS-01; §3.4.1 |
| `original_plate` | `VARCHAR(50)` | Biển số gốc (lưu dự phòng để hiển thị) | Duplicate từ User Service | FR-RES-05 |
| `normalized_plate` | `VARCHAR(50)` | Biển số chuẩn hóa. Dùng cho LPR matching và chống book trùng | Duplicate từ User Service | FR-CAP-04 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại xe | Từ profile xe | FR-RES-01 |
| `expected_start_time` | `TIMESTAMPTZ NOT NULL` | Giờ dự kiến bắt đầu đỗ. Quyết định khi nào trigger Protection Window và Allocation | Driver chọn | §3.4.4; §3.4.6 |
| `expected_end_time` | `TIMESTAMPTZ NOT NULL` | Giờ dự kiến rời đi | Driver chọn | §3.4.6 |
| `confirmed_at` | `TIMESTAMPTZ NULL` | Thời điểm reservation được xác nhận thanh toán/phân bổ | Trigger cập nhật | §3.4.3 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT'` | Trạng thái business theo §3.4.2 (PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, EXPIRED, NO_SHOW, CANCELLED, UNFULFILLABLE) | Lifecycle logic | §3.4.2; FR-RES-12 |
| `hold_expires_at` | `TIMESTAMPTZ NULL` | Hạn chót thanh toán (default ~5p). Quá hạn → status = `EXPIRED` | `NOW() + hold_duration` | §3.4.3 |
| `cancelled_at` | `TIMESTAMPTZ NULL` | Thời điểm hủy | Gán khi cancel | §3.4.10 |
| `accepted_pricing_snapshot` | `JSONB NULL` | Bản sao chính sách giá tại thời điểm book. Giữ nguyên giá trị dù biểu giá gốc bị Owner đổi (C-22) | Snapshot từ Parking Service | FR-POL-05; FR-PAY-10 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo booking. Nếu có tranh chấp start time, hệ thống lấy giờ này làm ưu tiên 2 | Database tự tạo | §3.4.6; FR-RES-13 |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_reservations` | `UNIQUE (id, tenant_id, site_id)` | Hỗ trợ composite FK |
| `chk_vehicle_type` | `CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE'))` | Loại xe hợp lệ |
| `chk_status` | `CHECK (status IN ('PENDING_PAYMENT', 'CONFIRMED', 'ALLOCATED', 'PARKING', 'COMPLETED', 'EXPIRED', 'NO_SHOW', 'CANCELLED', 'UNFULFILLABLE'))` | Lifecycle state hợp lệ |
| `chk_valid_times` | `CHECK (expected_end_time > expected_start_time)` | Giờ kết thúc phải sau giờ bắt đầu |
| `chk_exclusive_mode` | `CHECK (NOT (target_slot_id IS NOT NULL AND target_spatial_unit_id IS NOT NULL))` | Chống việc một booking vừa chọn đích danh Slot lại vừa chọn Zone |
| `no_overlapping_reservations_per_vehicle` | `EXCLUDE USING gist (...)` | **Chống Double-Booking cứng**: Không cho phép 1 xe đặt 2 lịch đè lên nhau tại cùng 1 bãi nếu booking đang active |
| `reservation_confirmation_time` | `TRIGGER BEFORE INSERT OR UPDATE` | Tự động ghi nhận thời điểm reservation được `CONFIRMED` |
| `idx_reservations_tenant_site` | `(tenant_id, site_id)` | Lọc booking theo bãi |
| `idx_reservations_account` | `(account_id)` | Lọc theo driver |
| `idx_reservations_status_times` | `(status, expected_start_time, expected_end_time)` | Tối ưu hóa background job (tìm các chuyến sắp tới Allocation Lead Time, Protection Window) |
| `idx_reservations_plate` | `(normalized_plate)` | Tối ưu LPR camera matching nhanh |

---

## 4. Bảng `parking_sessions` — Phiên đỗ xe thực tế

### 4.1 Mục đích nghiệp vụ

Bảng này chứa truth duy nhất về **sự kiện xe thực sự ở trong bãi**. Nó bắt đầu khi xe qua cổng (entry_time) và kết thúc khi xe ra khỏi cổng (exit_time). Phí đỗ xe cuối cùng (FR-PAY-11) được tính dựa trên actual entry/exit timestamp của bảng này, không phụ thuộc vào expected times của reservation.

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-GATE-03** | C | Record actual entry event và parking session riêng biệt với gate command |
| **FR-GATE-05** | C | Validate exit identity; record actual departure; release physical occupation |
| **FR-GATE-09** | C | Prevent duplicate open sessions; active session lookup bằng ticket |
| **FR-PAY-11** | C | Final price uses actual recorded entry and exit timestamps |
| **US-O01** | US | Operator check in / ghi nhận session mới |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh phiên đỗ xe | Tự sinh | — |
| `reservation_id` | `UUID NULL` | Trỏ về `reservations(id)`. `NULL` nếu là khách vãng lai (walk-in) | Link lúc check-in | §3.4.11 |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ | Context | FR-GATE-03 |
| `access_item_id` | `UUID NULL` | Logical ID trỏ sang Parking Service (ví dụ: thẻ nhựa được phát cho session này). `NULL` nếu dùng LPR 100% không thẻ | Quét tại cổng | FR-GATE-01 |
| `account_id` | `UUID NULL` | Account liên kết. Điền sẵn nếu từ reservation hoặc app scan. `NULL` cho guest/walk-in | Link account | FR-GATE-08 |
| `current_slot_id` | `UUID NULL` | Logical ID của physical slot xe đang đỗ (nếu bãi có sensor từng ô). `NULL` nếu chỉ check-in cổng ngoài | Cập nhật từ IoT/Operator | FR-GATE-05 |
| `original_plate` | `VARCHAR(50)` | Biển số raw lúc check-in (LPR nhận diện hoặc Operator nhập) | LPR / Operator | FR-GATE-01 |
| `normalized_plate` | `VARCHAR(50)` | Biển số chuẩn hóa. Dùng chống trùng session | App format | FR-GATE-09 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại xe | IoT / Khai báo | FR-GATE-03 |
| `ticket_reference` | `VARCHAR(100)` | Mã vé giấy/điện tử, dùng cho xe máy không biển (FR-VEH-03) hoặc fallback | Sinh lúc cấp phát | FR-GATE-07 |
| `entry_time` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Timestamp **thực tế** xe qua cổng vào | Database/Gateway | FR-PAY-11 |
| `entry_image_url` | `VARCHAR(500)` | Ảnh LPR camera chụp lúc vào làm bằng chứng giải quyết tranh chấp | IoT upload | FR-INC-07 |
| `exit_time` | `TIMESTAMPTZ NULL` | Timestamp thực tế rời bãi. `NULL` nếu chưa rời | Gate checkout | FR-PAY-11 |
| `exit_image_url` | `VARCHAR(500)` | Ảnh lúc ra làm bằng chứng | IoT upload | FR-INC-07 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái session. `ACTIVE`: đang đỗ. `PENDING_PAYMENT`: ra chờ thanh toán. `COMPLETED`: xong. `OVERSTAYED`: ở quá hạn. `DISPUTED`: có tranh chấp identity | Cập nhật luồng ra cổng | FR-GATE-05; FR-GATE-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi | Database | — |

### 4.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_parking_sessions` | `UNIQUE (id, tenant_id, site_id)` | Hỗ trợ composite FK |
| `chk_vehicle_type` | `CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE'))` | Loại xe hợp lệ |
| `chk_status` | `CHECK (status IN ('ACTIVE', 'PENDING_PAYMENT', 'COMPLETED', 'OVERSTAYED', 'DISPUTED'))` | Lifecycle state của session |
| `chk_exit_time` | `CHECK (exit_time IS NULL OR exit_time > entry_time)` | Giờ ra phải sau giờ vào |
| `fk_session_reservation` | `FOREIGN KEY (reservation_id, tenant_id, site_id) REFERENCES reservations(id, tenant_id, site_id) ON DELETE SET NULL` | Ràng buộc phiên đỗ với reservation gốc. Xóa booking thì session thành khách vãng lai (hoặc mồ côi) |
| `idx_parking_sessions_active_ticket` | `UNIQUE INDEX (ticket_reference) WHERE status = 'ACTIVE' AND ticket_reference IS NOT NULL` | **Chống trùng xe**: Đảm bảo 1 mã vé chỉ được dùng cho 1 session đang active |
| `idx_parking_sessions_active_access` | `UNIQUE INDEX (access_item_id) WHERE status = 'ACTIVE' AND access_item_id IS NOT NULL` | **Chống trùng thẻ**: Đảm bảo 1 thẻ vật lý chỉ được dùng cho 1 session đang active |
| `idx_parking_sessions_tenant_site` | `(tenant_id, site_id)` | Lọc session theo bãi |
| `idx_parking_sessions_account` | `(account_id)` | Tra cứu lịch sử của Driver |
| `idx_parking_sessions_status` | `(status)` | Phục vụ list các xe đang ở trong bãi (status=ACTIVE) |
| `idx_parking_sessions_plate` | `(normalized_plate)` | Tra cứu session bằng biển số |
| `idx_parking_sessions_ticket` | `(ticket_reference)` | Tra cứu session bằng mã vé |

---

## 5. Bảng `slot_allocations` — Gán tài nguyên vật lý

### 5.1 Mục đích nghiệp vụ

Thực thi logic **Allocation Ranking** (§3.4.6). Tại thời điểm `Allocation Lead Time` (VD: 30 phút trước giờ đến), Reservation Service chạy thuật toán ưu tiên (ranking) để cấp cứng một physical slot cho reservation. Bảng này quản lý việc gán đó. Nếu mất slot khẩn cấp, tài xế sẽ được allocate sang slot khác, tạo bản ghi mới tại đây.

### 5.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-RES-13** | C | Thực thi deterministic ranking (requested-slot → same-zone → fallback) |
| **FR-CAP-04** | A | Ensure concurrent claims cannot consume the same incompatible capacity (Chống Double-Booking) |
| **§3.4.6** | — | Allocation Lead Time, Reallocation logic |

### 5.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh việc gán slot | Tự sinh | — |
| `reservation_id` | `UUID NULL` | Reservation yêu cầu slot này | Allocation job | §3.4.6 |
| `parking_session_id` | `UUID NULL` | Nếu slot này được giữ cho xe walk-in (chưa reservation) | Gate checkout | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | — |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ | Context | — |
| `slot_id` | `UUID NOT NULL` | Logical ID của slot vật lý bên Parking Service | Kết quả Allocation ranking | FR-RES-13 |
| `allocated_start_time` | `TIMESTAMPTZ NOT NULL` | Thời điểm bắt đầu lock slot | Kế thừa expected_start | §3.4.6 |
| `allocated_end_time` | `TIMESTAMPTZ NOT NULL` | Thời điểm kết thúc lock slot | Kế thừa expected_end | §3.4.6 |
| `allocation_status` | `VARCHAR(50) NOT NULL DEFAULT 'RESERVED'` | `RESERVED`: đã xếp xong. `OCCUPIED`: tài xế đã đỗ vào ô. `RELEASED`: hủy booking hoặc bị re-allocate | Allocation/Gate sync | §3.4.6 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo record | Database | — |

### 5.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `chk_allocation_status` | `CHECK (allocation_status IN ('RESERVED', 'OCCUPIED', 'RELEASED'))` | Trạng thái phân bổ |
| `chk_valid_times` | `CHECK (allocated_end_time > allocated_start_time)` | Giờ kết thúc phải sau giờ bắt đầu |
| `fk_allocation_reservation` | `FOREIGN KEY (reservation_id, tenant_id, site_id) REFERENCES reservations(id, tenant_id, site_id) ON DELETE SET NULL` | Ràng buộc với booking |
| `fk_allocation_session` | `FOREIGN KEY (parking_session_id, tenant_id, site_id) REFERENCES parking_sessions(id, tenant_id, site_id) ON DELETE SET NULL` | Ràng buộc với session |
| `no_overlapping_slot_allocations` | `EXCLUDE USING gist (slot_id WITH =, TSTZRANGE(allocated_start_time, allocated_end_time) WITH &&) WHERE (allocation_status IN ('RESERVED', 'OCCUPIED'))` | **Chống Double-Booking Slot**: Ngăn 2 reservation/session đè lên nhau cùng 1 khoảng thời gian trên cùng 1 slot |
| `idx_slot_allocations_tenant_site` | `(tenant_id, site_id)` | Truy vấn theo bãi |
| `idx_slot_allocations_times` | `(allocated_start_time, allocated_end_time)` | Phục vụ tính toán conflict real-time |

---

## 6. Bảng `monthly_passes` — Vé tháng (Subscriptions)

> **[Merged từ `07-subscription-service-db.sql`]** — Đưa về cùng Reservation & Session vì vé tháng là entitlement dài hạn ảnh hưởng trực tiếp đến capacity (Guaranteed capacity) và liên quan đến việc khởi tạo parking session.

### 6.1 Mục đích nghiệp vụ

Quản lý vé tháng trả trước (rolling prepaid anchored periods - M-01~09). Có hai chế độ entitlement:
1. `WHEN_SPACE_AVAILABLE`: Chỉ được vào nếu bãi còn chỗ trống (giống walk-in).
2. `GUARANTEED_CAPACITY_SLOT`: Chắc chắn có 1 slot hoặc 1 slot cụ thể được fix cố định, system sẽ bảo vệ capacity này.

### 6.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **M-01–09** | C | Monthly passes: MVP rolling prepaid anchored periods |
| **§3.5.1** | — | Monthly entitlement types: when-space-available, guaranteed capacity/slot |
| **Appx F** | — | Quyền lợi vé tháng không tự sinh slot nếu đầy |

### 6.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh vé tháng | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Context | Cập nhật từ request | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Context | Cập nhật từ request | §3.2.1 |
| `account_id` | `UUID NOT NULL` | Logical ID (User Service) của người mua vé | Cập nhật từ request | M-01 |
| `pass_code` | `VARCHAR(100) NOT NULL UNIQUE` | Mã in trên thẻ/hóa đơn | Gen tự động | — |
| `entitlement_type` | `VARCHAR(50) NOT NULL` | `WHEN_SPACE_AVAILABLE` (như guest) hoặc `GUARANTEED_CAPACITY_SLOT` (fix cứng chỗ) | User/Owner đăng ký | §3.5.1; Appx F |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại xe vé tháng áp dụng | Request | §3.2.1 |
| `normalized_plate` | `VARCHAR(50)` | Biển số chuẩn hóa (1 vé gán 1 biển cụ thể) | Request | M-01 |
| `valid_from` | `TIMESTAMPTZ NOT NULL` | Thời điểm bắt đầu hiệu lực | Thanh toán xong set | M-09 |
| `valid_until` | `TIMESTAMPTZ NOT NULL` | Khung thời gian hiệu lực rolling 1 tháng/quý | Thanh toán xong set hạn | M-09 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | `PENDING_PAYMENT`, `ACTIVE`, `EXPIRED`, `CANCELLED` | Vòng đời thanh toán | M-01 |
| `target_spatial_unit_id` | `UUID NULL` | Zone ID fix (dùng khi Guaranteed Capacity) | Cấu hình | Appx F |
| `target_slot_id` | `UUID NULL` | Slot ID fix cụ thể (dùng khi Guaranteed Slot) | Cấu hình | Appx F |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo record | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ update | Application | — |

### 6.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_monthly_passes` | `UNIQUE (id, tenant_id, site_id)` | Hỗ trợ composite keys |
| `chk_entitlement_type` | `CHECK (entitlement_type IN ('WHEN_SPACE_AVAILABLE', 'GUARANTEED_CAPACITY_SLOT'))` | Quy định loại pass |
| `chk_vehicle_type` | `CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE'))` | Xe hợp lệ |
| `chk_status` | `CHECK (status IN ('PENDING_PAYMENT', 'ACTIVE', 'EXPIRED', 'CANCELLED'))` | Vòng đời của vé tháng |
| `chk_valid_times` | `CHECK (valid_until > valid_from)` | Hạn phải đúng logic |
| `chk_guaranteed_target` | `CHECK ((entitlement_type = 'WHEN_SPACE_AVAILABLE' AND target_spatial_unit_id IS NULL AND target_slot_id IS NULL) OR (entitlement_type = 'GUARANTEED_CAPACITY_SLOT' AND (target_spatial_unit_id IS NOT NULL OR target_slot_id IS NOT NULL)))` | Chặn cấu hình sai: Vé "chỗ trống" không được có đích, vé "fix chỗ" bắt buộc phải có đích |
| `no_overlapping_passes_per_vehicle` | `EXCLUDE USING gist (tenant_id WITH =, site_id WITH =, normalized_plate WITH =, tstzrange(valid_from, valid_until) WITH &&) WHERE (status = 'ACTIVE')` | **Chống mua trùng vé**: Không cho phép 1 xe có 2 vé tháng đang active đè lên nhau tại cùng 1 bãi |
| `idx_monthly_passes_tenant_site` | `(tenant_id, site_id)` | Filter vé theo bãi |
| `idx_monthly_passes_account` | `(account_id)` | Truy xuất tất cả vé của user |

---

## 7. Bản sao trạng thái cấu trúc (Local Cache / Read-Model)

### 6.1 Mục đích nghiệp vụ

Đây là các bảng Local Cache (Replication) được cập nhật thông qua Event-Driven Architecture (Kafka/RabbitMQ) từ Parking Service. 
Chúng giúp Reservation Service nhanh chóng kiểm tra trạng thái vật lý của bãi đỗ (ví dụ: bãi có đang active không, có đang bị khóa để sửa chữa không, chỗ đỗ có bị gỡ bỏ không) mà không cần gọi API đồng bộ sang Parking Service, đảm bảo tính Decoupling và Performance cho hệ thống Microservices. Tuyệt đối không dùng DB Trigger để can thiệp.

### 6.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **System Design** | N/A | Local Cache / Read-model phục vụ Event-Driven Architecture |

### 6.3 Bảng thuộc tính chi tiết

**Bảng structure_edit_holds** (Lưu cờ tạm dừng đặt chỗ khi bãi đang bảo trì)

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| site_id | UUID PK | ID bãi đỗ |
| 	enant_id | UUID NOT NULL | Logical ID doanh nghiệp |
| 	oken | UUID NOT NULL UNIQUE | Token khóa |
| created_at | TIMESTAMPTZ NOT NULL DEFAULT now() | Thời điểm khóa |

**Bảng structure_site_state** (Lưu trạng thái hoạt động của bãi đỗ)

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| site_id | UUID PK | ID bãi đỗ |
| 	enant_id | UUID NOT NULL | Logical ID doanh nghiệp |
| ctive | BOOLEAN NOT NULL | Trạng thái cho phép hoạt động |

**Bảng structure_removed_resources** (Lưu danh sách chỗ đỗ/khu vực đã bị xóa vật lý)

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| id | UUID PK | ID của resource bị xóa |
| site_id | UUID NOT NULL | ID bãi đỗ |
| kind | TEXT NOT NULL | Phân loại resource (slot hoặc unit) |

### 6.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| chk_kind | CHECK(kind IN ('slot','unit')) | Loại resource bị xóa hợp lệ |



## 8. Tóm tắt quan hệ giữa các bảng

```text
reservations (1) ──── (0..1) parking_sessions     [reservation_id]
reservations (1) ──── (0..N) slot_allocations     [reservation_id]

parking_sessions (1) ──── (0..N) slot_allocations [parking_session_id]

monthly_passes (N) ──── (1) site_capacity_pools   [logic implicit: guaranteed capacity]
```
*(Ghi chú: `parking_sessions` có thể không có `reservation` nếu là khách vãng lai walk-in. Allocation có thể đến từ Session nếu là phân phối thủ công từ cổng vào mà không book trước)*.

---

## 9. Tóm tắt Cross-service References

| Cột | Bảng hiện tại | Trỏ đến Service (Bảng tham chiếu) | Cơ chế đồng bộ |
|---|---|---|---|
| `account_id` | `reservations`, `parking_sessions`, `monthly_passes` | User Service (`users.id`) | Logical ID, lấy snapshot data lúc tạo booking |
| `normalized_plate` | `reservations`, `parking_sessions`, `monthly_passes` | User Service (`vehicles.normalized_plate`) | Duplicate cache để query nhanh, LPR send data để match |
| `tenant_id`, `site_id` | Tất cả bảng | Parking Service (`tenants.id`, `parking_sites.id`) | Logical IDs context block |
| `spatial_unit_id`, `target_spatial_unit_id` | `site_capacity_pools`, `reservations`, `monthly_passes` | Parking Service (`spatial_units.id`) | API validation / Event listener từ bãi đỗ |
| `slot_id`, `target_slot_id` | `slot_allocations`, `reservations`, `monthly_passes`, `parking_sessions` | Parking Service (`parking_slots.id`) | Cấp trực tiếp qua Ranking thuật toán |
| `access_item_id` | `parking_sessions` | Parking Service (`access_items.id`) | Gateway publish `AccessItemScanned` → Session tạo link qua Logical ID |

---

## 10. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key. |
| `btree_gist` | Bắt buộc phải có để sử dụng constraint `EXCLUDE USING gist` với các cột kết hợp `(=)` (B-Tree) và khoảng thời gian `(&&)` (GiST Range), giúp chặn đè lịch cứng cáp ở cấp Database. |
