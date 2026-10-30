# Payment & Pricing Service — Database Documentation

> **Service**: Payment & Pricing Service (`PaymentDb`)  
> **Database file**: [`04-payment-pricing-service-db.sql`](../../../../scripts/database/microservices/04-payment-pricing-service-db.sql)  
> **Recorded schema baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9); target alignment DB28/DB32 below is pending.
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý toàn bộ vòng đời tài chính, biểu giá và giao dịch dòng tiền: Biểu giá động (`tariffs`), Lịch sử chính sách giá bất biến (`policy_revisions`), Đơn hàng thanh toán (`payment_orders`), Giao dịch thanh toán (`payment_transactions`), Hóa đơn kế toán (`invoices`), Yêu cầu hoàn tiền (`refund_requests`), và Webhook cổng thanh toán payOS (`provider_webhooks`).

## v1.1 target alignment (DB28/DB32)

Payment owns tariffs and financial terms for monthly/yearly Driver products; Operations owns plans/coverage. Yearly periods are 12 calendar months, one period in advance, with separate configurable financial/refund policies; no refund percentage or renewal default is approved. Explicit Owner product enablement is separate from price. Existing financial columns/subject labels do not prove annual support. Discovery exposes current tariff plus billing unit without a made-up total when no interval exists; interval selection uses a source-backed estimated total. Preserve settled rounding, payment/entitlement timing, refund limits and unknown-outcome reconciliation. See [alignment backlog](../alignment-backlog.md#v11-schema-and-seed-alignment-pending). All DDL columns/defaults below remain recorded schema facts; no migration or runtime validation is claimed.

## 0. Deployment profile

| Hạng mục | Giá trị |
|---|---|
| Database vật lý | `smartpark_payment` (PostgreSQL 16, port `5444`) |
| Base DDL | [`04-payment-pricing-service-db.sql`](../../../../scripts/database/microservices/04-payment-pricing-service-db.sql) |
| Overlay | Không |
| Deployment status | Khớp arch `9a13499`; không có deviation |

---

## 1. Tổng quan kiến trúc & Quy tắc Định giá v1.0

Payment & Pricing Service là **trung tâm quản lý biểu giá và tiền tệ** của hệ sinh thái SmartPark. Theo kiến trúc chuẩn hóa v1.0, toàn bộ quyền sở hữu bảng biểu giá (`tariffs`) và snapshot giá đã được di dời triệt để từ Parking Service sang Payment Service.

```text
Driver / payOS Webhook → API Gateway
                             ↓
         Payment & Pricing Service DB (PaymentDb)
         ┌──────────────────────────────────────┐
         │ tariffs                              │ ← Biểu giá động theo loại xe
         │ policy_revisions (Immutable)         │ ← Snapshot giá bất biến
         │ payment_orders                       │ ← Đơn hàng thanh toán
         │ payment_transactions                 │ ← Giao dịch (payOS, tiền mặt)
         │ invoices                             │ ← Hóa đơn kế toán
         │ refund_requests                      │ ← Yêu cầu hoàn tiền
         │ provider_webhooks                    │ ← Idempotent webhook receiver
         └──────────────────────────────────────┘
```

### 1.1 Quy tắc Ân hạn Miễn phí 15 Phút (Free Parking Grace Rule)
Theo quy chuẩn SRS v1.0 RC3:
- **Thời lượng $\le 15$ phút:** Toàn bộ phiên đỗ thông thường được miễn phí (0 VND).
- **Thời lượng $> 15$ phút:** Thu phí toàn bộ thời gian không được bao trả, **tuyệt đối không trừ đi 15 phút đầu**.
- **Xe có vé tháng (Monthly Pass):** Đã được bao trả trong thời hạn gói, chỉ tính phí cho khoảng thời gian đỗ vượt ngoài khung giờ/phạm vi bảo trợ.
- **Quy tắc làm tròn tiền:** Tính tổng chi phí chưa làm tròn, sau đó làm tròn một lần duy nhất theo chuẩn **half-up đến hàng 1.000 VND**.

### 1.2 Nguyên tắc Lịch sử Giá Bất biến (`policy_revisions`)
Mọi thay đổi biểu giá không được phép UPDATE đè lên phiên bản cũ và tăng số nguyên `version`. Mọi lần thay đổi đều phải tạo một bản ghi `policy_revisions` bất biến (Immutable Record) với đầy đủ mốc thời gian `effective_from` để phục vụ đối soát giao dịch và giải quyết khiếu nại trong quá khứ.

---

## 2. Bảng `tariffs` — Biểu giá bãi xe

### 2.1 Mục đích nghiệp vụ
Định nghĩa bảng giá gửi xe theo loại phương tiện (CAR, MOTORCYCLE, OVERSIZED) và thời lượng đỗ (Block đầu, Block tiếp theo, khung giờ ngày/đêm) của bãi xe.

### 2.2 Bảng thuộc tính chi tiết
| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh biểu giá |
| `tenant_id`, `site_id` | `UUID NOT NULL` | Bãi xe áp dụng |
| `name` | `VARCHAR(100) NOT NULL` | Tên bảng giá |
| `vehicle_type` | `VARCHAR(50) NOT NULL` | CAR, MOTORCYCLE, OVERSIZED |
| `free_parking_grace_minutes` | `INT NOT NULL DEFAULT 15` | Cửa sổ ân hạn miễn phí (mặc định 15 phút) |
| `tariff_rules` | `JSONB NOT NULL` | Quy tắc tính giá theo block giờ |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` | Trạng thái áp dụng |

---

## 3. Bảng `policy_revisions` — Lịch sử chính sách giá bất biến

### 3.1 Mục đích nghiệp vụ
Lưu trữ toàn bộ các phiên bản chính sách giá trong lịch sử. Cấm tuyệt đối thao tác UPDATE.

### 3.2 Bảng thuộc tính chi tiết
| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ |
|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi revision |
| `policy_id` | `UUID NOT NULL` | ID biểu giá hoặc chính sách gốc |
| `policy_type` | `VARCHAR(50) NOT NULL` | TARIFF, SUBSCRIPTION, OVERSTAY_PENALTY, SPECIAL_EVENT |
| `revision_number` | `INT NOT NULL CHECK (>= 1)` | Số thứ tự phiên bản tăng dần |
| `effective_from`, `effective_to` | `TIMESTAMPTZ` | Khoảng thời gian áp dụng hiệu lực |
| `scope_type`, `scope_id` | `VARCHAR(50)`, `UUID` | Phạm vi áp dụng (GLOBAL, TENANT, SITE, ZONE) |
| `policy_values` | `JSONB NOT NULL` | Snapshot toàn bộ giá trị cấu hình tại thời điểm đó |
| `created_by` | `UUID NOT NULL` | Người ban hành chính sách |

---

## 4. Bảng `payment_orders` — Đơn hàng thanh toán

Quản lý đơn hàng phát sinh từ đặt chỗ trước, phiên đỗ tại cổng, hoặc gia hạn vé tháng. Hỗ trợ trường `idempotency_key` chống tạo đơn trùng lặp.

---

## 5. Bảng `payment_transactions` — Giao dịch dòng tiền

Lưu vết giao dịch thanh toán qua cổng trực tuyến (payOS, VNPay, MoMo, ZaloPay) hoặc tiền mặt (CASH) do Operator thu tại cổng. Có ràng buộc duy nhất `idx_provider_transaction_unique` trên `(payment_method, gateway_transaction_id)`.

### 5.1 Luồng thanh toán khi check-out

Check-out không được đóng phiên đỗ trước khi giao dịch thành công:

1. Operator gọi pricing để tính phí tại thời điểm hiện tại.
2. Payment Service tạo `payment_orders` với `PENDING` và thời hạn 15 phút.
3. Với tiền mặt, Operator xác nhận tại quầy; với payOS, frontend mở `checkout_url` và
   đối soát transaction sau khi quay lại hoặc refresh trang.
4. Chỉ khi transaction ở trạng thái `SUCCESS`, frontend mới gọi API check-out của
   OperationService.

`Idempotency-Key` của order giữ cho refresh/back không tạo đơn mới. Trạng thái order
và transaction là nguồn sự thật bền vững. Nút `demo-pay` chỉ dùng cho môi trường demo;
production vẫn phải chờ webhook hoặc đối soát payOS.

---

## 6. Bảng `invoices` — Hóa đơn kế toán

Lưu trữ hóa đơn thanh toán chính thức xuất cho khách hàng. Thiết kế v1.0 mở rộng `reference_type` (`PARKING_SESSION`, `RESERVATION`, `MONTHLY_PASS`) để xuất hóa đơn cho cả đặt cọc và vé tháng mà không bắt buộc phải có `session_id`.

---

## 7. Bảng `refund_requests` — Yêu cầu hoàn tiền

Quản lý luồng hoàn tiền cho các đơn hủy đặt chỗ hoặc khiếu nại cước phí (`REFUND_REQUESTED` -> `REFUND_APPROVED` -> `REFUND_COMPLETED`).

---

## 8. Bảng `provider_webhooks` — Hook nhà cung cấp thanh toán

Lưu trữ payload webhook nhận được từ payOS/VNPay với `provider_event_id` duy nhất đảm bảo tính idempotent tuyệt đối khi xử lý bất đồng bộ.

---

## 9. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id` | Tất cả bảng | User & Access Service (`UserDb.tenants.id`) | Logical ID |
| `account_id` | `payment_orders` | User & Access Service (`UserDb.accounts.id`) | Logical ID người trả tiền |
| `site_id` | Tất cả bảng | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID bãi xe |
| `reference_id` | `payment_orders`, `invoices` | OperationsDb (`reservations.id` hoặc `parking_sessions.id`) | Logical ID căn cứ thu tiền |
