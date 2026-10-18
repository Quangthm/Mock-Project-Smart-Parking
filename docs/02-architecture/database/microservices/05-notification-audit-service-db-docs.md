# Notification & Audit Service — Database Documentation

> **Service**: Notification & Audit Service (`NotificationAuditDb`)  
> **Database file**: [`05-notification-audit-service-db.sql`](../../../../scripts/database/microservices/05-notification-audit-service-db.sql)  
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý động cơ chuyển phát thông báo bền vững (`durable_notifications`), mẫu nội dung (`notification_templates`), token thiết bị (`push_device_tokens`), module hỗ trợ chung và tiếp nhận yêu cầu khôi phục tài khoản (`support_tickets`, `support_ticket_messages`), và hồ chiếu dữ liệu kiểm toán tập trung (`central_audit_projections`).

---

## 1. Tổng quan kiến trúc & Ranh giới v1.0

Notification & Audit Service là **hạ tầng truyền thông bất đồng bộ và kiểm toán an ninh** của toàn bộ hệ thống SmartPark theo chuẩn kiến trúc v1.0 RC3. Service này giải quyết 3 trách nhiệm cốt lõi:
1. **Chuyển phát thông báo bền vững (Durable Notification Engine):** Nhận sự kiện từ RabbitMQ (`PaymentVerified`, `ReservationCreated`, `AccountLocked`), thực hiện chống gửi trùng (Deduplication qua `idempotency_key`), và lưu vết lịch sử chuyển phát đa kênh (IN_APP, EMAIL, SMS, PUSH).
2. **Module Hỗ trợ chung (General Support Module):** Tiếp nhận khiếu nại cước phí, phản ánh sự cố bãi, và đặc biệt là ca tiếp nhận khôi phục tài khoản người dùng (`ACCOUNT_RECOVERY`). Lưu ý: Support module chỉ tiếp nhận và lưu lịch sử xét duyệt ca hỗ trợ, **không** có quyền ghi đè bảng mật khẩu bên UserDb.
3. **Hồ chiếu kiểm toán tập trung (Central Audit Projections):** Nhận projection log kiểm toán từ toàn bộ 5 service khác bắn qua RabbitMQ để phục vụ tra cứu bảo mật tập trung cho System Administrator.

```text
Other Services (Event Publishers) → RabbitMQ
                                      ↓
           Notification & Audit Service DB (NotificationAuditDb)
           ┌──────────────────────────────────────────────────┐
           │ notification_templates                           │ ← Quản lý Template đa kênh
           │ push_device_tokens                               │ ← Lưu trữ FCM / APNS Tokens
           │ durable_notifications                            │ ← Động cơ gửi tin bền vững
           │ support_tickets                                  │ ← Tiếp nhận khiếu nại & Recovery
           │ support_ticket_messages                          │ ← Trao đổi tin nhắn hỗ trợ
           │ central_audit_projections (Zero Cascade)         │ ← Chiếu kiểm toán tập trung
           └──────────────────────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi v1.0:**
- **Decoupled Failure:** Thất bại trong việc gửi email/SMS/thông báo không bao giờ được phép làm gián đoạn hay rollback giao dịch nghiệp vụ cốt lõi ở các service gốc.
- **Idempotency & Deduplication:** Mọi yêu cầu gửi tin đều mang `idempotency_key` duy nhất để chống gửi đúp khi RabbitMQ redelivery.
- **Audit Trails Immutability:** Bảng `central_audit_projections` là append-only, cấm tuyệt đối mọi hình thức UPDATE và không áp dụng CASCADE.

---

## 2. Bảng `notification_templates` — Mẫu thông báo

Lưu trữ cấu trúc và nội dung tĩnh của các thông báo (IN_APP, EMAIL, SMS, PUSH), hỗ trợ placeholder biến động (`{{user_name}}`, `{{plate}}`).

---

## 3. Bảng `push_device_tokens` — Token thiết bị

Lưu trữ FCM/APNS token cho các thiết bị di động của người dùng để bắn Push Notification.

---

## 4. Bảng `durable_notifications` — Chuyển phát thông báo bền vững

Quản lý toàn bộ tiến trình gửi tin từ hàng đợi: `originating_event`, `idempotency_key`, `recipient_id`, `channel`, `attempts`, `status IN ('PENDING', 'PROCESSING', 'DELIVERED', 'FAILED', 'DEAD_LETTER')`, và `last_error`.

---

## 5. Bảng `support_tickets` — Tiếp nhận hỗ trợ & Account Recovery

Tiếp nhận các yêu cầu hỗ trợ người dùng theo các danh mục: `GENERAL_INQUIRY`, `PAYMENT_DISPUTE`, `ACCOUNT_RECOVERY`, `FACILITY_COMPLAINT`, `SYSTEM_BUG`. Quản lý trạng thái từ `OPEN` -> `IN_PROGRESS` -> `RESOLVED` -> `CLOSED`.

---

## 6. Bảng `support_ticket_messages` — Tin nhắn trao đổi hỗ trợ

Lưu vết các tin nhắn đối thoại giữa khách hàng và nhân viên hỗ trợ (Admin/Operator) kèm tệp đính kèm (`attachments`).

---

## 7. Bảng `central_audit_projections` — Hồ chiếu kiểm toán tập trung (Zero Cascade)

Chiếu toàn bộ dữ liệu kiểm toán từ các service trong hệ sinh thái (User, Facility, Operations, Payment, IoT) để phục vụ Admin điều tra vi phạm. Tuyệt đối không áp dụng CASCADE.

---

## 8. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `recipient_id`, `user_id`, `requester_id` | Tất cả bảng | User & Access Service (`UserDb.users.id`) | Logical ID |
| `site_id` | `support_tickets` | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID |
| `event_id` | `central_audit_projections` | Tất cả các service phát sự kiện | Correlation ID từ Message Broker |
