# IoT & Recognition Service — Database Documentation

> **Service**: IoT & Recognition Service (`IoTDb`)  
> **Database file**: [`06-iot-recognition-service-db.sql`](../../../../scripts/database/microservices/06-iot-recognition-service-db.sql)  
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit (Inter-service) & MQTT (Thiết bị IoT)  
> **Scope**: Quản lý danh mục thiết bị ngoại vi và cổng barrier (`devices`), ghi nhận sự kiện nhận diện biển số và bảo tồn chứng cứ media (`lpr_events`), lưu trữ dữ liệu cảm biến ô đỗ (`sensor_telemetries`), và tích hợp toàn diện **Module Trợ lý ảo AI** (`assistant_conversations`, `assistant_messages`, `assistant_tool_runs`).

## 0. Deployment profile

| Hạng mục | Giá trị |
|---|---|
| Database vật lý | `smartpark_iot` (PostgreSQL 16, port `5446`) |
| Base DDL | [`06-iot-recognition-service-db.sql`](../../../../scripts/database/microservices/06-iot-recognition-service-db.sql) |
| Overlay | Không |
| Deployment status | Khớp arch `9a13499`; không có deviation |

---

## 1. Tổng quan kiến trúc & Ranh giới v1.0

IoT & Recognition Service là **giác quan thu thập dữ liệu và xử lý trí tuệ nhân tạo** của hệ thống SmartPark theo chuẩn kiến trúc v1.0 RC3. Service này sở hữu 2 phân hệ cốt lõi:
1. **Phân hệ IoT & Recognition:** Giao tiếp qua MQTT với camera và cảm biến, thu nhận quan sát thô, thực hiện OCR nhận diện biển số, quản lý bằng chứng hình ảnh (Media evidence contract), và chuyển phát sự kiện `ObservationReceived` / `RecognitionRecorded` lên RabbitMQ cho Parking Operations Service xử lý. IoT Service **không bao giờ tự ý cấp quyền mở cổng hay xác nhận phiên đỗ**.
2. **Module Trợ lý ảo AI (AI Assistant Module):** Quản lý phiên hội thoại người dùng (`assistant_conversations`), nội dung tin nhắn (`assistant_messages`), và lịch sử các lần gọi tool tra cứu API (`assistant_tool_runs`).

```text
Camera / Sensor / Barrier (MQTT)          Chat Web / Mobile (HTTP)
               │                                      │
               ▼                                      ▼
    IoT & Recognition Service (IoTDb)      AI Assistant Module (IoTDb)
    ┌───────────────────────────────┐      ┌───────────────────────────┐
    │ devices                       │      │ assistant_conversations   │
    │ lpr_events (Zero Evidence Loss│      │ assistant_messages        │
    │ sensor_telemetries            │      │ assistant_tool_runs       │
    └───────────────────────────────┘      └───────────────────────────┘
                    │                                  │
                    └───────────────┬──────────────────┘
                                    ▼
                          RabbitMQ + MassTransit
```

**Nguyên tắc thiết kế cốt lõi v1.0:**
- **Bảo tồn chứng cứ độc lập với thiết bị (Zero Evidence Loss):** Khóa ngoại `device_id` trong `lpr_events` dùng `ON DELETE SET NULL`. Khi thiết bị camera bị tháo dỡ hoặc thay thế, toàn bộ lịch sử ảnh chụp và biển số đã nhận diện vẫn được bảo toàn nguyên vẹn phục vụ đối soát và xử lý khiếu nại.
- **Xử lý kết quả không chắc chắn (Uncertain Recognition Outcomes):** Biển số có thể bị mờ, lóa đèn hoặc không có biển số. Trường `recognition_outcome` bắt buộc hỗ trợ các trạng thái không chắc chắn: `SUCCESS`, `UNREADABLE`, `MULTIPLE_CANDIDATES`, `TIMEOUT`, `NO_PLATE`, `MANUAL_REVIEW`.
- **Media Lifecycle Contract:** Lưu trữ `media_asset_id` và `media_retention_class` (`STANDARD_30_DAYS`) theo quy chuẩn lưu trữ media của hệ thống.

---

## 2. Bảng `devices` — Danh sách thiết bị ngoại vi

Quản lý thông tin kết nối và định danh của các thiết bị phần cứng (Camera LPR, Barrier Gate, Slot Sensor) tại các bãi đỗ xe: mã thiết bị (`device_code`), loại (`CAMERA_LPR`, `SENSOR`, `BARRIER_GATE`), IP/MAC và trạng thái kết nối (`UNKNOWN`, `ONLINE`, `OFFLINE`, `MAINTENANCE`).

---

## 3. Bảng `lpr_events` — Sự kiện nhận diện biển số & Chứng cứ Media

### 3.1 Mục đích nghiệp vụ
Lưu trữ toàn bộ ảnh chụp và kết quả nhận diện biển số xe tại cổng ra/vào. Cho phép Operator hiệu chỉnh thủ công (`corrected_plate`, `corrected_by`, `corrected_at`) khi OCR nhận diện sai.

### 3.2 Bảng thuộc tính chi tiết
| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh sự kiện LPR |
| `device_id` | `UUID NULL REFERENCES devices(id) ON DELETE SET NULL` | Camera chụp (cho phép NULL khi upload ảnh thủ công) |
| `tenant_id`, `site_id` | `UUID NOT NULL` | Bãi xe diễn ra sự kiện |
| `original_plate`, `normalized_plate` | `VARCHAR(50) NULL` | Biển số nhận diện (có thể NULL nếu không thấy biển) |
| `confidence_score` | `DECIMAL(5,2) NULL` | Độ tin cậy OCR (0 - 100%) |
| `recognition_outcome` | `VARCHAR(50) NOT NULL` | SUCCESS, UNREADABLE, MULTIPLE_CANDIDATES, TIMEOUT, NO_PLATE, MANUAL_REVIEW |
| `model_version` | `VARCHAR(50) NULL` | Phiên bản AI model nhận diện |
| `image_url` | `VARCHAR(500) NULL` | Đường dẫn ảnh chụp phương tiện |
| `media_asset_id` | `UUID NULL` | ID định danh tài nguyên media bền vững |
| `media_retention_class` | `VARCHAR(50) NOT NULL DEFAULT 'STANDARD_30_DAYS'` | Chính sách lưu trữ dữ liệu (30 ngày) |
| `direction` | `VARCHAR(50) NOT NULL` | INBOUND (vào) hoặc OUTBOUND (ra) |
| `processed_status` | `VARCHAR(50) NOT NULL DEFAULT 'UNPROCESSED'` | UNPROCESSED, SYNCED_TO_OPERATIONS |
| `captured_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm camera chụp ảnh |

---

## 4. Bảng `sensor_telemetries` — Cảm biến trạng thái ô đỗ

Ghi nhận tín hiệu cảm biến siêu âm/từ trường gắn tại từng ô đỗ (`slot_id`) để xác định ô đang có xe hay trống (`is_occupied`) theo thời gian thực.

---

## 5. Bảng `assistant_conversations` — Phiên hội thoại Trợ lý ảo AI

Quản lý phiên tương tác của người dùng (`user_id`) với chatbot AI hỗ trợ tìm bãi, tra cứu bảng giá, hoặc hướng dẫn nạp tiền. Lưu trữ ngữ cảnh bãi xe và xe đang hỏi (`context_scope JSONB`).

---

## 6. Bảng `assistant_messages` — Tin nhắn Trợ lý ảo AI

Lưu vết toàn bộ tin nhắn trong cuộc hội thoại: vai trò người gửi (`sender_role IN ('USER', 'ASSISTANT', 'SYSTEM', 'TOOL')`), nội dung câu trả lời (`content`), và số lượng token tiêu thụ (`tokens_count`).

---

## 7. Bảng `assistant_tool_runs` — Nhật ký gọi tool của AI

Lưu vết quá trình AI gọi các tool API (ví dụ: `search_parking_lots`, `get_tariff_info`): tên tool (`tool_name`), tham số truyền vào (`input_parameters JSONB`), kết quả trả về (`output_result JSONB`), và trạng thái thực thi (`status IN ('PENDING', 'SUCCESS', 'FAILED')`).

---

## 8. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id`, `site_id` | Tất cả bảng | Facility & Visualization (`FacilityDb`) | Logical ID |
| `slot_id` | `sensor_telemetries` | Facility & Visualization (`FacilityDb.parking_slots.id`) | Logical ID |
| `user_id` | `assistant_conversations` | User & Access Service (`UserDb.users.id`) | Logical ID |
