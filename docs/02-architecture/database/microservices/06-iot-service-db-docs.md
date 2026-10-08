# IoT Service — Database Documentation

> **Service**: IoT Service (Device Gateway & Recognition Service)  
> **Database file**: [`06-iot-service-db.sql`](../../../../scripts/database/microservices/06-iot-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý danh mục thiết bị phần cứng (camera, barrier, sensor), ghi nhận sự kiện nhận diện biển số (LPR events), và lưu trữ lịch sử trạng thái vật lý của từng ô đỗ xe (Telemetry).

---

## 1. Tổng quan kiến trúc

IoT Service đóng vai trò là "Giác quan và Tay chân" của hệ thống SmartPark. Nó giao tiếp trực tiếp với phần cứng tại bãi đỗ thông qua giao thức mạng nội bộ, thu thập dữ liệu thô (ảnh biển số, sóng siêu âm), chuẩn hóa, và publish thành các Business Events (ví dụ: `VehicleArrivedAtGate`, `SlotOccupied`) lên Kafka cho các service khác xử lý.

```text
Physical Devices (Cameras/Sensors) → TCP/MQTT/HTTP
                                         ↓
                                 IoT Service DB
                 ┌──────────────────────────────────────────────┐
                 │ devices                                      │ ← Quản lý danh mục thiết bị
                 │   ├── lpr_events                             │ ← Lịch sử nhận diện biển số
                 │   └── sensor_telemetries                     │ ← Lịch sử cảm biến chỗ đỗ
                 └──────────────────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- **High-Volume Telemetry**: Dữ liệu cảm biến và nhận diện được sinh ra liên tục. DB này tối ưu cho việc ghi (Write-heavy) và làm nguồn tham chiếu (Evidence) thay vì lưu trạng thái business (Business state lưu bên Parking/Reservation Service).
- **Physical Isolation**: Các service khác không biết IP hay MAC của thiết bị. Chúng chỉ giao tiếp qua Device Logical ID hoặc Slot Logical ID.
- **Dữ liệu bằng chứng (Evidence-based)**: Ảnh chụp biển số và lịch sử đóng cắt cảm biến được dùng để giải quyết khiếu nại (FR-INC-07) và kiểm toán.

---

## 2. Bảng `devices` — Danh sách thiết bị

### 2.1 Mục đích nghiệp vụ

Quản lý thông tin kết nối và định danh của toàn bộ thiết bị phần cứng (Camera LPR, Barrier Gate, Slot Sensor) tại các bãi đỗ xe. Bảng này hỗ trợ tính năng "Health Check" để biết thiết bị nào đang sập (OFFLINE) để điều phối nhân sự xử lý (FR-OPS-03).

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-OPS-03** | C | Khi thiết bị offline/lỗi, trạng thái vật lý chuyển sang UNKNOWN. |
| **§6.2.1** | — | Device Gateway xử lý physical-adapter scope. |
| **US-OW02** | US | Owner cấu hình bãi đỗ và thiết bị. |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất thiết bị | Tự sinh bởi database | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp quản lý | Context từ luồng cấu hình | FR-LOT-01 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ xe thiết bị được lắp đặt | Context từ luồng cấu hình | — |
| `device_code` | `VARCHAR(100) NOT NULL` | Mã thiết bị do Owner quy định (VD: `CAM-IN-01`) | Owner nhập | — |
| `device_type` | `VARCHAR(50) NOT NULL` | Phân loại: `CAMERA_LPR`, `SENSOR`, `BARRIER_GATE` | Owner nhập | §6.2.1 |
| `location_desc` | `VARCHAR(255) NULL` | Mô tả vị trí lắp (VD: Cổng vào tầng 1) | Owner nhập | — |
| `ip_address` | `VARCHAR(45) NULL` | Địa chỉ IP tĩnh của thiết bị (Hỗ trợ IPv4/IPv6) | Owner cấu hình network | §6.2.1 |
| `mac_address` | `VARCHAR(45) NULL` | Địa chỉ MAC | Network scan / Nhập tay | — |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'UNKNOWN'` | Tình trạng: `UNKNOWN`, `ONLINE`, `OFFLINE`, `MAINTENANCE` | Cập nhật qua ping/heartbeat | FR-OPS-03 |
| `last_ping_at` | `TIMESTAMPTZ NULL` | Thời điểm gần nhất thiết bị kết nối thành công | Heartbeat job | FR-OPS-03 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ update | Application | — |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_devices_tenant_site` | `UNIQUE (id, tenant_id, site_id)` | Hỗ trợ composite keys (ví dụ cho RLS) |
| `uq_device_code_site` | `UNIQUE (tenant_id, site_id, device_code)` | Mã thiết bị phải là duy nhất trong cùng 1 bãi đỗ |
| `chk_device_type` | `CHECK (device_type IN ('CAMERA_LPR', 'SENSOR', 'BARRIER_GATE'))` | Hỗ trợ các loại phần cứng MVP quy định |
| `chk_device_status` | `CHECK (status IN ('UNKNOWN', 'ONLINE', 'OFFLINE', 'MAINTENANCE'))` | Lifecycle kết nối của thiết bị |
| `idx_devices_tenant_site` | `(tenant_id, site_id)` | Filter danh sách thiết bị theo bãi |

---

## 3. Bảng `lpr_events` — Lịch sử nhận diện biển số

### 3.1 Mục đích nghiệp vụ

Bảng này hoạt động như một bộ đệm (buffer) và nhật ký bằng chứng (evidence log). Khi Camera nhận diện được một biển số, dữ liệu OCR thô được lưu vào đây trước. Sau đó, Application sẽ bắn event `VehicleArrivedAtGate` sang Reservation & Session Service để bắt đầu/kết thúc phiên đỗ xe (FR-GATE-01).

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-GATE-01** | R/X | Nhận diện phương tiện (LPR/QR fallback). Cần threshold/equality processing. |
| **FR-INC-01 / FR-GATE-07** | C | Sử dụng bằng chứng ảnh chụp từ cổng để ghi nhận sự cố (FR-INC-01) và recover session (FR-GATE-07) |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh event nhận diện | Tự sinh | — |
| `device_id` | `UUID NOT NULL` | Trỏ về camera thực hiện chụp ảnh | Data từ hardware | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp | Inherit từ device | — |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ | Inherit từ device | — |
| `original_plate` | `VARCHAR(50) NOT NULL` | Biển số gốc OCR đọc được (có thể có ký tự lạ/dấu cách) | Thuật toán OCR | FR-GATE-01 |
| `normalized_plate` | `VARCHAR(50) NOT NULL` | Biển số đã được làm sạch (chỉ giữ chữ và số) để match logic | Threshold/equality processing | FR-GATE-01 |
| `confidence_score` | `DECIMAL(5,2) NULL` | Tỉ lệ chính xác (%) do mô hình AI trả về. Operator có thể cảnh báo nếu score thấp | Thuật toán OCR | C-14 |
| `image_url` | `VARCHAR(500) NULL` | Link ảnh gốc (lưu trên Blob Storage) làm bằng chứng giải quyết dispute | Hardware SDK | FR-INC-01; FR-GATE-07 |
| `direction` | `VARCHAR(50) NOT NULL` | `INBOUND` (xe đi vào) hoặc `OUTBOUND` (xe đi ra) | Cấu hình chiều camera | FR-GATE-05 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'UNPROCESSED'` | `UNPROCESSED` (chưa xử lý), `SYNCED_TO_PARKING` (đã ném Kafka event thành công) | Worker flow | — |
| `captured_at` | `TIMESTAMPTZ NOT NULL` | Giờ chụp thực tế lấy từ đồng hồ của Camera (để loại trừ độ trễ mạng) | Timestamp thiết bị | FR-GATE-03 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ server nhận được data | Database | — |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `fk_lpr_device` | `FOREIGN KEY (device_id, tenant_id, site_id) REFERENCES devices(id, tenant_id, site_id) ON DELETE CASCADE` | Xóa thiết bị thì xóa log (hoặc có thể đổi thành `SET NULL` nếu muốn giữ audit dài hạn, DDL hiện là CASCADE) |
| `chk_confidence_score` | `CHECK (confidence_score >= 0 AND confidence_score <= 100)` | Điểm tin cậy phải nằm trong dải phần trăm |
| `chk_direction` | `CHECK (direction IN ('INBOUND', 'OUTBOUND'))` | Camera tại cổng chỉ quay được 1 trong 2 chiều |
| `chk_lpr_status` | `CHECK (status IN ('UNPROCESSED', 'SYNCED_TO_PARKING'))` | Trạng thái đồng bộ event |
| `idx_lpr_events_tenant_site` | `(tenant_id, site_id)` | Truy xuất log camera của 1 bãi |
| `idx_lpr_events_plate_normalized` | `(normalized_plate)` | Tra cứu lịch sử ra/vào của 1 xe bằng biển số (FR-GATE-08) |
| `idx_lpr_events_captured_at` | `(captured_at)` | Hỗ trợ xóa data cũ định kỳ theo policy |

---

## 4. Bảng `sensor_telemetries` — Cảm biến trạng thái chỗ đỗ

### 4.1 Mục đích nghiệp vụ

Lưu trữ log tín hiệu dạng Timeseries từ các cảm biến sóng âm (Ultrasonic) lắp tại từng ô đỗ (`parking_slots`). Data này chứng minh một chiếc xe đã đỗ vào ô lúc nào và rời ô lúc nào. Nó cung cấp bằng chứng cho việc kiểm tra Đỗ sai vị trí (FR-INC-03) mà không cần dùng Camera AI đắt tiền cho từng slot.

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-OPS-03** | C | Nếu sensor mất tín hiệu, slot physical state sẽ là UNKNOWN. Bảng này lưu trạng thái known cuối cùng. |
| **FR-INC-03** | C | Operator đối chiếu tín hiệu sensor và luồng allocation để kết luận xe đỗ sai ô. |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi đo kiểm | Tự sinh | — |
| `device_id` | `UUID NOT NULL` | Cảm biến nào gửi tín hiệu này | Data từ hardware | — |
| `tenant_id` | `UUID NOT NULL` | Doanh nghiệp | Inherit từ device | — |
| `site_id` | `UUID NOT NULL` | Bãi đỗ xe | Inherit từ device | — |
| `slot_id` | `UUID NOT NULL` | Logical ID trỏ sang `parking_slots.id` thuộc Parking Service | Owner cấu hình mapping | FR-INC-03 |
| `is_occupied` | `BOOLEAN NOT NULL` | `true` (có vật thể che cảm biến), `false` (trống) | Hardware signal | — |
| `detected_at` | `TIMESTAMPTZ NOT NULL` | Giờ cảm biến phát hiện thay đổi (chỉ gửi log khi có thay đổi trạng thái) | Timestamp thiết bị | — |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ server lưu trữ | Database | — |

### 4.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `fk_sensor_device` | `FOREIGN KEY (device_id, tenant_id, site_id) REFERENCES devices(id, tenant_id, site_id) ON DELETE CASCADE` | Ràng buộc với cảm biến vật lý |
| `idx_sensor_telemetries_tenant_site` | `(tenant_id, site_id)` | Filter log theo bãi đỗ |
| `idx_sensor_telemetries_slot_id` | `(slot_id)` | **Cực kỳ quan trọng**: Truy xuất lịch sử đỗ của 1 ô cụ thể để giải quyết tranh chấp đè slot |
| `idx_sensor_telemetries_detected_at` | `(detected_at)` | Cleanup dữ liệu Timeseries |

---

## 5. Tóm tắt quan hệ giữa các bảng

```text
devices (1) ──── (N) lpr_events             [Các ảnh chụp LPR từ Camera]
devices (1) ──── (N) sensor_telemetries     [Các log sóng siêu âm từ Sensor]
```

---

## 6. Tóm tắt Cross-service References

| Cột | Bảng hiện tại | Trỏ đến Service (Bảng tham chiếu) | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id`, `site_id` | Tất cả bảng | Parking Service (`tenants.id`, `parking_sites.id`) | Logical ID, context block |
| `slot_id` | `sensor_telemetries` | Parking Service (`parking_slots.id`) | Mapping cứng từ cấu hình Admin. Khi sensor có data, IoT publish Kafka để Parking Update `physical_state` của slot |
| `normalized_plate` | `lpr_events` | Reservation Service (`reservations`, `parking_sessions`) | Cung cấp đầu vào (Plate) để Reservation Service match Booking hoặc sinh Guest Session mới (FR-GATE-01) |

---

## 7. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key. |
