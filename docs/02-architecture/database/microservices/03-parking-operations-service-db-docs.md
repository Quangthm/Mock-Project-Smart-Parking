# Parking Operations Service — Database Documentation

> **Service**: Parking Operations Service (`OperationsDb`)  
> **Database file**: [`03-parking-operations-service-db.sql`](../../../../scripts/database/microservices/03-parking-operations-service-db.sql)  
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý toàn bộ vòng đời vận hành động của hệ thống bãi xe: Đặt chỗ trước (`reservations`), phiên đỗ xe thực tế (`parking_sessions`), gán ô đỗ (`slot_allocations`), cấu hình hạn mức sức chứa (`operational_capacity_configs`), chính sách thời gian vận hành (`operational_policies`), hạn ngạch slot dự phòng (`structure_backup_policies`), vật phẩm định danh thẻ từ/QR (`access_items`), vé tháng khi có chỗ (`monthly_passes`), sự cố ca trực (`operational_incidents`, `operator_incident_events`), module tìm kiếm bãi xe (`search_facility_projections`), và nhật ký cổng (`audit_logs`).

---

## 1. Tổng quan kiến trúc & Thuật toán Sức chứa v1.0

Parking Operations Service là **trái tim vận hành động** của hệ sinh thái SmartPark theo kiến trúc chuẩn hóa v1.0 RC3. Service này sở hữu toàn bộ các sự kiện diễn ra theo thời gian thực (real-time facts).

```text
Driver / Operator / IoT Entry → API Gateway
                                    ↓
             Parking Operations Service DB (OperationsDb)
             ┌──────────────────────────────────────────────┐
             │ operational_capacity_configs                 │ ← Cấu hình hạn mức
             │ operational_policies                         │ ← Cửa sổ thời gian chính sách
             │ structure_backup_policies                    │ ← Hạn ngạch dự phòng
             │ reservations                                 │ ← Đặt chỗ (Time-range GiST)
             │ parking_sessions                             │ ← Phiên đỗ thực tế
             │ slot_allocations                             │ ← Gán slot chống đụng độ
             │ access_items                                 │ ← Thẻ từ / QR ra vào
             │ monthly_passes                               │ ← Vé tháng (When-space-available)
             │ operational_incidents & events               │ ← Sự cố ca trực
             │ search_facility_projections                  │ ← Module Search & Discovery
             │ audit_logs (Zero Cascade)                    │ ← Nhật ký cổng ra vào
             └──────────────────────────────────────────────┘
```

### 1.1 Thuật toán Kiểm tra Sức chứa theo Khoảng Thời gian (Interval Overlap)
- **Vấn đề của bộ đếm phẳng (Scalar Counter Flaw):** Trong các đề xuất cũ v0.9, hệ thống dùng một biến số nguyên `reserved_count = 200` để cộng dồn số lượng đặt chỗ. Cách làm này hoàn toàn sai sót khi áp dụng cho các ngày khác nhau (ví dụ: 100 lượt đặt ngày thứ Hai và 100 lượt đặt ngày thứ Ba là hoàn toàn hợp lệ và không hề xung đột sức chứa).
- **Giải pháp v1.0:** Toàn bộ việc kiểm tra khả dụng phải dựa trên **khoảng thời gian chồng lấn (Time-range overlaps)** sử dụng kiểu dữ liệu `TSTZRANGE` và chỉ mục không gian `GiST`.

### 1.2 Công thức Khả dụng theo Lý thuyết Tập hợp (Set Union Formula)
Theo đặc tả v1.0 RC3, các tài nguyên không khả dụng tại một thời điểm $t$ phải được tính toán theo phép **hợp (Union)** của các tập hợp trạng thái, ngăn chặn tuyệt đối tình trạng trừ đúp một tài nguyên:
$$\text{Unavailable}(t) = \text{Occupied}(t) \cup \text{Protected}(t) \cup \text{Pending\_Payment}(t) \cup \text{Backup}(t)$$
$$\text{Available}(t) = \text{Total} - |\text{Unavailable}(t)|$$

*Ví dụ thực tế:* Nếu bãi có tổng 10 chỗ: 3 chỗ đang có xe đỗ (`Occupied`), 2 chỗ giữ làm dự phòng (`Backup`), trong đó có 1 chỗ dự phòng hiện đang có xe thực tế đỗ. Khi đó $|	ext{Unavailable}| = 4$ (chứ không phải $3 + 2 = 5$). Số chỗ khả dụng còn lại là $10 - 4 = 6$.

---

## 2. Bảng `operational_capacity_configs` — Định mức sức chứa vận hành

### 2.1 Mục đích nghiệp vụ
Lưu trữ hạn mức nhận đặt trước tối đa (`max_reservation_quota`) và tổng sức chứa vật lý thiết kế cho từng khu vực/loại xe phục vụ việc tính toán khả dụng tức thời.

### 2.2 Bảng thuộc tính chi tiết
| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh cấu hình |
| `tenant_id` | `UUID NOT NULL` | Logical Reference |
| `site_id` | `UUID NOT NULL` | Bãi xe áp dụng |
| `spatial_unit_id` | `UUID NULL` | Khu vực áp dụng (NULL = toàn bãi) |
| `vehicle_type` | `VARCHAR(50) NOT NULL` | CAR, MOTORCYCLE, OVERSIZED |
| `total_physical_capacity` | `INT NOT NULL CHECK (>= 0)` | Sức chứa vật lý tối đa |
| `max_reservation_quota` | `INT NOT NULL CHECK (>= 0)` | Hạn mức cho phép đặt chỗ trước |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` | Trạng thái hiệu lực |

---

## 3. Bảng `operational_policies` — Tham số thời gian vận hành

### 3.1 Mục đích nghiệp vụ
Quản lý các thông số thời gian chuẩn hóa theo SRS v1.0:
- `arrival_late_tolerance_minutes`: 15 phút ân hạn trễ giờ đến.
- `payment_hold_duration_minutes`: 15 phút giữ chỗ chờ thanh toán.
- `min_reservation_duration_minutes`: 15 phút thời lượng đặt tối thiểu.
- `max_reservation_duration_hours`: 24 giờ thời lượng đặt tối đa.
- `reservation_protection_window_hours`: 4 giờ bảo vệ chỗ đỗ.
- `allocation_lead_time_minutes`: 60 phút trước giờ đến sẽ tự động gán slot cụ thể.

---

## 4. Bảng `structure_backup_policies` — Hạn ngạch slot dự phòng

Quản lý số lượng chỗ đỗ cần bảo lưu làm dự phòng cho sự cố hoặc trường hợp khẩn cấp (`backup_count`), được tiếp nhận từ FacilityDb sang OperationsDb.

---

## 5. Bảng `reservations` — Đặt chỗ trước

### 5.1 Mục đích nghiệp vụ
Quản lý các yêu cầu đặt chỗ trước của tài xế. Ngăn chặn đặt trùng xe qua GiST exclusion constraint `no_overlapping_reservations_per_vehicle`. Sử dụng `version` cho Optimistic Concurrency Control.

### 5.2 Bảng thuộc tính chi tiết
| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK` | Định danh đặt chỗ |
| `tenant_id`, `site_id`, `account_id` | `UUID NOT NULL` | Logical references |
| `target_spatial_unit_id`, `target_slot_id` | `UUID NULL` | Mục tiêu đặt chỗ |
| `normalized_plate`, `vehicle_type` | `VARCHAR` | Biển số xe và loại xe |
| `expected_start_time`, `expected_end_time`| `TIMESTAMPTZ NOT NULL` | Khung thời gian đặt chỗ |
| `status` | `VARCHAR(50) NOT NULL` | PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING, COMPLETED, EXPIRED, NO_SHOW, CANCELLED |
| `version` | `INT NOT NULL DEFAULT 1` | Chống xung đột đồng thời (OCC) |

---

## 6. Bảng `parking_sessions` — Phiên đỗ xe thực tế

Lưu trữ thông tin xe thực tế đỗ tại bãi: thời điểm vào (`entry_time`), ảnh chụp biển số vào (`entry_image_url`), thời điểm ra (`exit_time`), ảnh chụp ra (`exit_image_url`), thẻ/mã vé (`access_item_id`, `ticket_reference`), và trạng thái phiên đỗ (`ACTIVE`, `PENDING_PAYMENT`, `COMPLETED`, `OVERSTAYED`, `DISPUTED`).

---

## 7. Bảng `slot_allocations` — Gán tài nguyên ô đỗ chống Double-Booking

Đảm bảo một ô đỗ vật lý (`slot_id`) không bao giờ bị gán trùng lặp cho hai xe khác nhau trong cùng một khoảng thời gian thông qua ràng buộc loại trừ không gian `no_overlapping_slot_allocations`.

---

## 8. Bảng `access_items` — Thẻ từ & Mã QR ra vào

Tiếp nhận từ Facility: Quản lý vòng đời của thẻ từ RFID/mã QR định danh ra vào cổng (`card_number`, `status IN ('AVAILABLE', 'IN_USE', 'LOST', 'DISABLED')`, `current_session_id`).

---

## 9. Bảng `monthly_passes` — Vé tháng

Theo quyết định v1.0, vé tháng chỉ áp dụng hình thức **WHEN_SPACE_AVAILABLE** (miễn trừ biểu phí khi bãi còn chỗ, không bảo đảm giữ slot cố định khi xe đang ở ngoài bãi). Loại bỏ hoàn toàn hình thức `GUARANTEED_CAPACITY_SLOT`.

---

## 10. Bảng `operational_incidents` & `operator_incident_events`

Quản lý sự cố ca trực: mất vé (`LOST_TICKET`), trạng thái slot không rõ (`UNKNOWN_SLOT_STATE`), lệch dữ liệu thanh toán lúc ra (`CHECKOUT_MISMATCH`).

---

## 11. Bảng `search_facility_projections` — Module Tìm kiếm & Khám phá

Bảng chiếu dữ liệu (Projection Cache) phục vụ tìm kiếm bãi xe theo tọa độ GPS bán kính, trạng thái mở cửa, và số chỗ trống gần nhất mà không cần gọi query trực tiếp xuyên database.

---

## 12. Bảng `audit_logs` — Nhật ký kiểm toán cổng (Zero Cascade)

Ghi nhận toàn bộ thao tác mở barie, cưỡng chế gán slot, đổi trạng thái phiên đỗ của Operator. Tuyệt đối không áp dụng CASCADE để lưu vết vĩnh viễn.

---

## 13. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id` | Tất cả bảng | User & Access Service (`UserDb.tenants.id`) | Logical ID |
| `account_id` | `reservations`, `parking_sessions`, `monthly_passes` | User & Access Service (`UserDb.accounts.id`) | Logical ID |
| `site_id` | Tất cả bảng | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID |
| `slot_id` | `slot_allocations`, `parking_sessions` | Facility & Visualization (`FacilityDb.parking_slots.id`) | Logical ID |
| `reservation_id` | Payment Service | OperationsDb (`reservations.id`) | Phục vụ thanh toán giữ chỗ |
| `parking_session_id` | Payment Service | OperationsDb (`parking_sessions.id`) | Phục vụ thanh toán khi checkout |
