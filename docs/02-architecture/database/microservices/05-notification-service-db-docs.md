# Notification Service — Database Documentation

> **Service**: Notification Service  
> **Database file**: [`05-notification-service-db.sql`](../../../../scripts/database/microservices/05-notification-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý mẫu thông báo (`notification_templates`), quản lý token thiết bị để gửi Push (`push_device_tokens`), và theo dõi lịch sử/trạng thái gửi thông báo đa kênh (`notification_logs`).

---

## 1. Tổng quan kiến trúc

Notification Service là một microservice cung cấp hạ tầng gửi tin nhắn bất đồng bộ qua nhiều kênh (Email, SMS, Push Notification). Các service khác (như User, Reservation, Payment) không gửi tin nhắn trực tiếp mà publish các sự kiện (Events) vào Kafka (ví dụ: `PaymentSuccess`, `OTPRequested`). Notification Service sẽ consume các sự kiện này, map với template tương ứng và gửi đi thông qua các provider (SendGrid, Twilio, Firebase).

```text
Other Services (Event Publishers) → Kafka
                                      ↓
                            Notification Service DB
                ┌──────────────────────────────────────────────┐
                │ notification_templates                       │ ← Quản lý Template
                │ push_device_tokens                           │ ← Lưu trữ FCM/APNS Tokens
                │ notification_logs                            │ ← Lưu lịch sử/Trạng thái gửi
                └──────────────────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- **Decoupled Delivery**: Lỗi khi gửi thông báo (VD: sai số điện thoại) không bao giờ được làm rollback hoặc ảnh hưởng đến logic nghiệp vụ cốt lõi ở các service khác (FR-RPT-01).
- **Idempotency theo Business Event**: Đảm bảo không gửi 1 thông báo 2 lần cho cùng một sự kiện, người nhận và kênh (FR-RPT-01).
- **Template Versioning**: Cho phép thay đổi nội dung thông báo nhưng vẫn giữ được lịch sử những gì đã gửi thực tế (bằng cách reference đúng template version).

---

## 2. Bảng `notification_templates` — Mẫu thông báo

### 2.1 Mục đích nghiệp vụ

Lưu trữ cấu trúc và nội dung tĩnh của các loại thông báo (Email, SMS, Push) dưới dạng các mẫu (templates). Bảng này cho phép Admin cập nhật nội dung thông báo linh hoạt (ví dụ: thay đổi câu chữ của email xác nhận đặt chỗ) mà không cần deploy lại code. Template hỗ trợ truyền biến (placeholders) như `{{user_name}}`.

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-RPT-01** | C | Record event ID, recipient, channel, **template/version**, attempt, delivery status and failure reason |
| **§3.6.1-3** | — | Các mục đích sử dụng cụ thể của từng kênh (Push, SMS, Email) đòi hỏi phải có nhiều mẫu khác nhau |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của template | Tự sinh bởi database | — |
| `template_code` | `VARCHAR(100) NOT NULL` | Mã template cố định mà Application gọi (VD: `PAYMENT_SUCCESS_EMAIL`) | Do Developer/Admin định nghĩa | FR-RPT-01 |
| `version` | `INT NOT NULL DEFAULT 1` | Phiên bản của template. Mỗi lần sửa nội dung sẽ sinh version mới để không ảnh hưởng lịch sử các log cũ | Tự tăng bởi Application khi update | FR-RPT-01 |
| `channel` | `VARCHAR(50) NOT NULL` | Kênh gửi của template (`EMAIL`, `SMS`, `PUSH`) | Admin cấu hình | FR-RPT-01 |
| `subject` | `VARCHAR(255) NULL` | Tiêu đề thông báo (dùng cho EMAIL hoặc PUSH) | Admin cấu hình | — |
| `body_content` | `TEXT NOT NULL` | Nội dung chính chứa placeholders | Admin cấu hình | — |
| `is_active` | `BOOLEAN NOT NULL DEFAULT true` | Đánh dấu version template này có đang được sử dụng hay không | Admin toggle | — |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo record | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ update | Application | — |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_template_version` | `UNIQUE(template_code, version)` | Đảm bảo mỗi version của một template_code là duy nhất |
| `chk_template_channel` | `CHECK (channel IN ('EMAIL', 'SMS', 'PUSH'))` | Ràng buộc các kênh giao tiếp chuẩn được hệ thống hỗ trợ |
| `idx_notification_templates_code` | `(template_code)` | Truy vấn nhanh khi hệ thống cần load template để gửi đi |

---

## 3. Bảng `push_device_tokens` — Thiết bị nhận Push Notification

### 3.1 Mục đích nghiệp vụ

Quản lý danh sách các thiết bị (Smartphone, Web Browser) của User để phục vụ việc đẩy Push Notification. Một User có thể đăng nhập trên nhiều thiết bị (VD: cả iPhone và Android), hệ thống cần gửi thông báo đến tất cả các token đang `is_active`.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **§3.6.1** | — | Push Notifications cho: Reservation confirmation, Payment reminders, Parking spot availability alerts, Promotional offers |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh record | Tự sinh bởi database | — |
| `user_id` | `UUID NOT NULL` | Logical ID trỏ sang bảng `users` bên User Service | Payload token truyền lên từ Client | §3.6.1 |
| `device_token` | `VARCHAR(255) NOT NULL` | Firebase Cloud Messaging (FCM) Token hoặc APNS Token do hệ điều hành cấp phát | Mobile App lấy từ OS và gửi lên Backend | §3.6.1 |
| `device_os` | `VARCHAR(50) NULL` | Phân loại hệ điều hành thiết bị (`IOS`, `ANDROID`, `WEB`) để xử lý format payload push nếu cần thiết | Mobile App gửi lên | — |
| `is_active` | `BOOLEAN NOT NULL DEFAULT true` | Cờ đánh dấu Token có còn hiệu lực không (sẽ set false nếu Push Provider trả về lỗi NotRegistered/InvalidToken) | Application tự update | — |
| `last_used_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cuối cùng token này được sử dụng thành công, dùng để dọn dẹp các token đã quá cũ (Inactive devices) | Update khi gửi push thành công | — |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo record | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ update | Application | — |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `chk_device_os` | `CHECK (device_os IN ('IOS', 'ANDROID', 'WEB'))` | Chỉ hỗ trợ Push qua các nền tảng xác định |
| `idx_push_device_tokens_user_id` | `(user_id)` | Dùng để query lấy toàn bộ Token của 1 User khi cần đẩy Push Notification cho họ |
| `idx_push_device_tokens_device_token` | `(device_token)` | Kiểm tra xem token này đã tồn tại trong DB chưa để tránh insert đúp khi User mở lại app |

---

## 4. Bảng `notification_logs` — Lịch sử gửi thông báo

### 4.1 Mục đích nghiệp vụ

Bảng đóng vai trò Audit Log chuyên sâu cho hạ tầng thông báo, đảm bảo có thể truy vết được (traceability) tình trạng gửi của từng thông điệp. Hệ thống sẽ dựa vào bảng này để thực hiện Retry nếu gặp lỗi network tạm thời (FAILED), và chống gửi đúp thông báo (Idempotency).

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-RPT-01** | C | Delivery failure is separate from the underlying business outcome... record event ID, recipient, channel, template/version, attempt, delivery status and failure reason |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh của một record gửi thông báo | Tự sinh bởi database | — |
| `target_user_id` | `UUID NULL` | Logical ID trỏ sang `users`. Có thể `NULL` (Ví dụ: gửi email cho Guest User không có tài khoản) | Metadata truyền từ event | — |
| `event_id` | `VARCHAR(100) NULL` | ID sự kiện nghiệp vụ gốc (Authoritative business event ID). VD: `PAYMENT-1234`. Dùng làm khóa idempotency | Payload từ Kafka Event | FR-RPT-01 |
| `template_code` | `VARCHAR(100) NULL` | Template đã được dùng để gen ra nội dung này | Lấy từ Notification_templates | FR-RPT-01 |
| `template_version` | `INT NULL` | Version của template vào thời điểm gen | Lấy từ Notification_templates | FR-RPT-01 |
| `channel` | `VARCHAR(50) NOT NULL` | Kênh đã sử dụng: `EMAIL`, `SMS`, `PUSH` | Logic xử lý event | FR-RPT-01 |
| `recipient` | `VARCHAR(255) NOT NULL` | Điểm đến thực tế: Địa chỉ Email, Số điện thoại, hoặc Device Token cụ thể | Trích xuất từ profile User hoặc request | FR-RPT-01 |
| `subject` | `VARCHAR(255) NULL` | Tiêu đề thực tế đã gửi sau khi bind params | Gen từ Template | — |
| `content` | `TEXT NULL` | Nội dung thực tế đã gửi sau khi bind params | Gen từ Template | — |
| `attempt_count` | `INT NOT NULL DEFAULT 1` | Số lần đã thử gửi. Nếu provider sập, hệ thống retry và tăng số này lên | Engine gửi thông báo | FR-RPT-01 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'PENDING'` | Trạng thái: `PENDING`, `SENT`, `FAILED` | Phản hồi từ Third-party Provider (Twilio/SendGrid) | FR-RPT-01 |
| `failure_reason` | `TEXT NULL` | Chi tiết mã lỗi hoặc exception message từ provider nếu gửi thất bại | Bắt Exception lúc gọi API | FR-RPT-01 |
| `sent_at` | `TIMESTAMPTZ NULL` | Thời điểm thực sự đẩy thành công qua mạng | Callback từ Provider | — |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo log ban đầu | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ update | Application | — |

### 4.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_notification_event_recipient_channel` | `UNIQUE(event_id, recipient, channel)` | **Idempotency Key**: Chống gửi đúp. Không bao giờ gửi 1 Event qua cùng 1 Kênh cho cùng 1 Người nhận lần thứ 2 |
| `chk_log_channel` | `CHECK (channel IN ('EMAIL', 'SMS', 'PUSH'))` | Giới hạn kênh thông báo |
| `chk_log_status` | `CHECK (status IN ('PENDING', 'SENT', 'FAILED'))` | Các trạng thái hợp lệ của giao dịch gửi |
| `chk_attempt_count` | `CHECK (attempt_count >= 1)` | Số lần gửi phải lớn hơn hoặc bằng 1 |
| `idx_notification_logs_target_user_id` | `(target_user_id)` | Phục vụ tính năng xem "Lịch sử thông báo" của riêng 1 User |
| `idx_notification_logs_status` | `(status)` | Phục vụ Background Job: Lọc nhanh các bản ghi `PENDING` hoặc `FAILED` để retry |
| `idx_notification_logs_event_id` | `(event_id)` | Phục vụ Audit: Trace xem một Business Event đã sinh ra những thông báo nào |
| `idx_notification_logs_created_at` | `(created_at)` | Phục vụ Audit và dọn dẹp data cũ định kỳ |

---

## 5. Tóm tắt quan hệ giữa các bảng

```text
notification_templates (1) ──── (N) notification_logs   [Thông qua code và version]

push_device_tokens (1) ──── (N) notification_logs       [Thông qua cột recipient = device_token]
```

---

## 6. Tóm tắt Cross-service References

| Cột | Bảng hiện tại | Trỏ đến Service (Bảng tham chiếu) | Cơ chế đồng bộ |
|---|---|---|---|
| `user_id` | `push_device_tokens` | User Service (`users.id`) | Logical ID, Client App (Mobile) đính kèm token khi gọi API lên backend |
| `target_user_id` | `notification_logs` | User Service (`users.id`) | Dữ liệu context truyền từ Event của các Service khác thông qua Kafka |
| `event_id` | `notification_logs` | Bất kỳ Service nào (vd: Payment, Reservation) | Authoritative Business Event ID được cấp phát ở gốc để làm mốc Idempotency (VD: ID của Order) |

---

## 7. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key. |
