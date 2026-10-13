# Payment Service — Database Documentation

> **Service**: Payment Service  
> **Database file**: [`04-payment-service-db.sql`](../../../../scripts/database/microservices/04-payment-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý vòng đời đơn hàng thanh toán (`payment_orders`), giao dịch dòng tiền (`payment_transactions`), phát hành hóa đơn (`invoices`), xử lý yêu cầu hoàn tiền (`refund_requests`), và tiếp nhận callback từ đối tác thanh toán (`provider_webhooks`).

---

## 1. Tổng quan kiến trúc

Payment Service đóng vai trò là "kế toán trưởng" của hệ thống SmartPark, chịu trách nhiệm xử lý toàn bộ các luồng tài chính (thu phí đỗ xe, phí giữ chỗ, vé tháng, hoàn tiền) thông qua tích hợp với các cổng thanh toán bên thứ ba (VNPay, MoMo, ZaloPay) và thu tiền mặt (CASH_COLLECT).

```text
Reservation/Gate Service → API Gateway
                               ↓
                       Payment Service DB
           ┌──────────────────────────────────────┐
           │ payment_orders                       │ ← Đơn hàng tổng (Order)
           │   ├── payment_transactions           │ ← Dòng tiền thực tế (Txn)
           │   ├── invoices                       │ ← Hóa đơn kế toán (PDF data)
           │   └── refund_requests                │ ← Luồng hoàn tiền
           │ provider_webhooks                    │ ← Callbacks từ Momo/VNPay
           └──────────────────────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- **Financial Immutability**: Tuyệt đối không xóa vật lý (không có DELETE) hoặc sửa đổi các giao dịch đã chốt (SUCCESS/FAILED/REFUNDED). 
- **Idempotency**: Chống thanh toán đúp bằng `idempotency_key` ở order và `gateway_transaction_id` ở mức transaction.
- **Tách bạch Order và Transaction**: Một `payment_order` có thể có nhiều `payment_transactions` (ví dụ: thanh toán thất bại lần 1, retry lần 2, hoặc hoàn tiền).
- **Asynchronous Webhook**: Webhook từ provider được lưu thô vào `provider_webhooks` trước khi process để đảm bảo không mất mát dữ liệu tài chính (Auditability).

---

## 2. Bảng `payment_orders` — Đơn hàng thanh toán

### 2.1 Mục đích nghiệp vụ

Đại diện cho một **yêu cầu thanh toán gốc** (intent to pay). Bảng này lưu trữ tổng số tiền cần thanh toán, loại phí (thuê bao, phí đỗ, v.v.), và trạng thái tổng quát. Nó không quan tâm đến phương thức thanh toán cụ thể, mà tập trung vào việc đối chiếu số tiền cuối cùng `final_amount = amount - discount - deposit`. Bất kỳ yêu cầu thanh toán nào (kể cả thu tiền mặt tại cổng) cũng phải sinh ra một Order.

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-05** | C | Phân biệt rõ pending, failed, unknown outcomes. Xử lý timeout/expiry hold của reservation |
| **FR-PAY-09** | C | Free-parking duration inclusive (nếu free thì amount = 0) |
| **FR-PAY-11** | C | Final parking price tính từ entry/exit thực tế |
| **§3.5.1** | — | Hỗ trợ Parking Fee, Monthly Pass Fee, Overnight Fee, Reservation Fee |
| **US-D02** | US | Tài xế thanh toán phí |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh đơn hàng | Tự sinh bởi database | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context từ request | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ xe | Context từ request | — |
| `account_id` | `UUID NULL` | Trỏ đến `users.id`. `NULL` nếu là khách vãng lai không tài khoản | Xác định qua session/reservation | — |
| `order_code` | `VARCHAR(100) NOT NULL` | Mã đơn hàng hiển thị cho User và đối soát với Provider (VD: `SP-PAY-202610-XYZ`) | Sinh tự động (Format do Application quyết) | FR-PAY-04 |
| `idempotency_key` | `VARCHAR(255) NULL` | Key chống tạo đúp Order từ cùng 1 request của client | Client gửi lên trong Header | FR-PAY-04 |
| `order_type` | `VARCHAR(50) NOT NULL` | Mục đích thu tiền: `PARKING_FEE`, `MONTHLY_PASS_FEE`, `OVERNIGHT_FEE`, `RESERVATION_FEE` | Phân loại luồng gọi từ Reservation Service | §3.5.1 |
| `reference_type` | `VARCHAR(50) NOT NULL` | Đối tượng gốc sinh ra khoản phí này: `RESERVATION` hoặc `PARKING_SESSION` | Liên kết nghiệp vụ | FR-PAY-02 |
| `reference_id` | `UUID NOT NULL` | Logical ID trỏ sang bảng tương ứng trong Reservation & Session Service | Context truyền sang | FR-PAY-02 |
| `amount` | `DECIMAL(12,2) NOT NULL` | Tổng tiền gốc cần thu | Trả về từ Engine tính phí | FR-PAY-10 |
| `discount_amount` | `DECIMAL(12,2) NOT NULL DEFAULT 0` | Số tiền được giảm giá (Voucher/Promo) | Áp dụng logic khuyến mãi | — |
| `deposit_deducted` | `DECIMAL(12,2) NOT NULL DEFAULT 0` | Số tiền đã cọc (ví dụ: phí Reservation) được khấu trừ vào phí đỗ cuối cùng | Truy xuất từ Reservation | — |
| `final_amount` | `DECIMAL(12,2) NOT NULL` | Số tiền cuối cùng user phải trả thực tế | = amount - discount - deposit | FR-PAY-10 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'PENDING'` | `PENDING`, `PAID`, `FAILED`, `CANCELLED`, `REFUNDED` | Cập nhật từ kết quả giao dịch | FR-PAY-05 |
| `expires_at` | `TIMESTAMPTZ NULL` | Thời điểm đơn hàng hết hạn thanh toán (dùng cho Reservation payment hold) | = now + payment_hold_duration | §3.4.3 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo đơn | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ cập nhật lần cuối | Application | — |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_payment_orders_tenant` | `UNIQUE (id, tenant_id)` | Hỗ trợ composite keys (ví dụ cho RLS) |
| `uq_payment_orders_code` | `UNIQUE (order_code)` | Mã đơn phải duy nhất toàn hệ thống |
| `uq_payment_orders_idempotency` | `UNIQUE (idempotency_key)` | **Idempotency**: Chặn duplicate payment request |
| `chk_order_type` | `CHECK (order_type IN ('PARKING_FEE', 'MONTHLY_PASS_FEE', 'OVERNIGHT_FEE', 'RESERVATION_FEE'))` | Loại phí theo §3.5.1 |
| `chk_reference_type` | `CHECK (reference_type IN ('RESERVATION', 'PARKING_SESSION'))` | Đối tượng gốc hợp lệ |
| `chk_amounts_positive` | `CHECK (amount >= 0)`, `CHECK (discount_amount >= 0)`, `CHECK (deposit_deducted >= 0)`, `CHECK (final_amount >= 0)` | Không cho phép số tiền âm |
| `chk_final_amount_calc` | `CHECK (final_amount = (amount - discount_amount - deposit_deducted))` | **Financial Integrity**: Đảm bảo công thức tính toán số cuối không bị sai lệch ở tầng application |
| `chk_order_status` | `CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED'))` | Vòng đời đơn hàng |
| `idx_payment_orders_tenant_site` | `(tenant_id, site_id)` | Lọc doanh thu theo bãi đỗ |
| `idx_payment_orders_account` | `(account_id)` | Liệt kê lịch sử thanh toán của 1 User |
| `idx_payment_orders_reference` | `(reference_type, reference_id)` | Lookup ngược từ Session/Reservation để tìm hóa đơn |

---

## 3. Bảng `payment_transactions` — Giao dịch dòng tiền

### 3.1 Mục đích nghiệp vụ

Quản lý **từng lượt giao dịch dòng tiền cụ thể** (nhận tiền, trả lại tiền) của một Payment Order. Một Order có thể thanh toán lỗi bằng VNPay, sau đó thanh toán thành công bằng MoMo (sinh ra 2 transactions). Quản lý Retry limit, Idempotency của đối tác bên thứ ba tại đây. Bảng này cũng ghi nhận việc thu tiền mặt (`CASH_COLLECT`) do Operator xác nhận.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-03** | C | Hỗ trợ VNPay, MoMo, ZaloPay và CASH_COLLECT thủ công qua Operator |
| **FR-PAY-04** | R/A | Verified provider results and idempotency |
| **FR-PAY-05** | C | Retries: Unknown không tính là retryable failure, cần reconciliation |
| **§3.5.3** | — | Retry mechanism, Provider webhooks, backend validation thay vì tin tưởng frontend |
| **US-O02** | US | Operator thu tiền mặt |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh giao dịch nội bộ | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | — |
| `payment_order_id` | `UUID NOT NULL` | Thuộc về Order gốc nào | Gán từ Order | — |
| `transaction_type` | `VARCHAR(50) NOT NULL` | `PAYMENT` (thu tiền) hoặc `REFUND` (chi tiền) | Sinh lúc khởi tạo giao dịch | FR-PAY-06 |
| `payment_method` | `VARCHAR(50) NOT NULL` | Kênh: `VNPAY`, `MOMO`, `ZALOPAY`, `CASH` | Do user/operator chọn | FR-PAY-03 |
| `gateway_transaction_id` | `VARCHAR(255) NULL` | Mã giao dịch do Provider (Momo, VNPay) cấp. Dùng để đối soát (Reconciliation). `NULL` nếu là `CASH` hoặc chưa gọi API sang đối tác thành công | Lưu từ response của Provider / Webhook | FR-PAY-04 |
| `amount` | `DECIMAL(12,2) NOT NULL` | Số tiền thực tế di chuyển trong giao dịch này | Gán theo Order | — |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'PENDING'` | `PENDING`, `SUCCESS`, `FAILED`, `CANCELLED`, `UNKNOWN` (cần đối soát thủ công) | Cập nhật từ kết quả Provider / Webhook | FR-PAY-05; §3.5.3 |
| `paid_at` | `TIMESTAMPTZ NULL` | Thời điểm giao dịch thực sự thành công theo backend provider | Provider trả về | FR-PAY-04 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo giao dịch | Database | — |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `fk_txn_order` | `FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT` | Không được xóa Order nếu đã có Transaction |
| `chk_transaction_type` | `CHECK (transaction_type IN ('PAYMENT', 'REFUND'))` | Phân loại dòng tiền vào/ra |
| `chk_payment_method` | `CHECK (payment_method IN ('VNPAY', 'MOMO', 'ZALOPAY', 'CASH'))` | Chỉ hỗ trợ các kênh MVP quy định trong §3.5.3 |
| `chk_txn_status` | `CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'UNKNOWN'))` | Trạng thái giao dịch (UNKNOWN quan trọng cho reconciliation) |
| `idx_payment_transactions_order_tenant` | `(payment_order_id, tenant_id)` | Tra cứu lịch sử dòng tiền của 1 đơn hàng |
| `idx_payment_transactions_gateway` | `(gateway_transaction_id)` | Đối soát nhanh với file Excel từ Provider |
| `idx_provider_transaction_unique` | `UNIQUE INDEX (payment_method, gateway_transaction_id) WHERE gateway_transaction_id IS NOT NULL` | **Chống duplicate từ đối tác**: Một mã giao dịch của đối tác (ví dụ MoMo) không được xuất hiện 2 lần trong hệ thống |

---

## 4. Bảng `invoices` — Hóa đơn kế toán

### 4.1 Mục đích nghiệp vụ

Bảng này phục vụ yêu cầu xuất Hóa đơn PDF (FR-PAY-07). Nó snapshot lại toàn bộ chi tiết tính phí (thời gian đỗ, block giá, phụ phí) vào khoảnh khắc thanh toán thành công để sau này hiển thị chính xác lịch sử cho người dùng, bảo vệ dữ liệu khỏi việc bảng giá (`tariffs`) bị sửa đổi trong tương lai (FR-POL-05).

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-07** | C | Generate PDF invoice, hiển thị payment history. Từ chối cross-account lookup |
| **FR-POL-05** | C | Record policy revisions và version applied to accepted transaction (Snapshot) |
| **FR-PAY-08** | A | Show overstay charges (Overstay penalty) |
| **§3.5.3** | — | Invoice generation in PDF format |
| **US-D06** | US | Tài xế xem lịch sử và hóa đơn đỗ xe |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi hóa đơn | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | — |
| `payment_order_id` | `UUID NOT NULL` | Hóa đơn xuất cho đơn hàng nào | Sinh sau khi Order `PAID` | — |
| `session_id` | `UUID NOT NULL` | Logical ID trỏ về `parking_sessions` liên quan | Gán từ Order reference | FR-PAY-07 |
| `site_id` | `UUID NOT NULL` | Bãi đỗ xe nào xuất hóa đơn | Context | — |
| `invoice_number` | `VARCHAR(100) NOT NULL UNIQUE` | Số hóa đơn hiển thị trên PDF (VD: `INV-202610-00123`) | Auto-increment / System gen | FR-PAY-07 |
| `total_parked_minutes` | `INT NOT NULL` | Tổng số phút đã đỗ tính từ entry đến exit. (Snapshot để minh bạch) | Engine tính phí | FR-PAY-11 |
| `base_parking_fee` | `DECIMAL(12,2) NOT NULL DEFAULT 0` | Phí đỗ xe cơ bản trước phụ thu | Engine tính phí | — |
| `overstay_penalty_fee` | `DECIMAL(12,2) NOT NULL DEFAULT 0` | Phụ phí ở lại quá giờ | Engine tính phí | FR-PAY-08 |
| `final_amount` | `DECIMAL(12,2) NOT NULL` | Tổng tiền thanh toán (bằng với payment_orders.final_amount) | Engine tính phí | — |
| `pricing_snapshot` | `JSONB NOT NULL` | **Bản sao độc lập** của rules bảng giá (`tariff_rules`) và policies đã dùng để tính tiền hóa đơn này. | Copy từ Parking Service lúc checkout | FR-POL-05; C-22 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ xuất hóa đơn | Database | — |

### 4.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `fk_invoice_order` | `FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT` | Hóa đơn bị khóa cứng với Order đã thanh toán |
| `uq_invoice_number` | `UNIQUE (invoice_number)` | Mã hóa đơn duy nhất |
| `idx_invoices_tenant_site` | `(tenant_id, site_id)` | Lọc xuất báo cáo kế toán (FR-RPT-02) |
| `idx_invoices_order_tenant` | `(payment_order_id, tenant_id)` | Tra cứu hóa đơn của 1 đơn hàng |
| `idx_invoices_session` | `(session_id)` | Tra cứu hóa đơn từ 1 Parking Session cụ thể |

---

## 5. Bảng `refund_requests` — Yêu cầu hoàn tiền

### 5.1 Mục đích nghiệp vụ

Quản lý luồng Khiếu nại/Hoàn tiền. Không giống tự động thanh toán, hoàn tiền cần phê duyệt (Approval workflow). Bảng này lưu vết quá trình duyệt hoàn tiền (FR-PAY-06), ai là người duyệt, lý do và bằng chứng.

### 5.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-06** | C | Full/partial audited refunds. Appeal → evidence → Operator approval → refund handling. Approval is distinct from actual payout |
| **FR-INC-05** | C | Appeal handling flow qua Incident |
| **§3.5.5** | — | Refund lifecycle: `REQUESTED` → `APPROVED` → `SUBMITTED` → `COMPLETED` |
| **US-D10/US-OW07** | US | Tài xế khiếu nại, Owner hoàn tiền |

### 5.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh yêu cầu | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Doanh nghiệp xử lý hoàn tiền | Context | — |
| `payment_order_id` | `UUID NOT NULL` | Trỏ về Order cần hoàn (Partial/Full) | Chọn từ hệ thống | FR-PAY-06 |
| `amount` | `DECIMAL(12,2) NOT NULL` | Số tiền yêu cầu hoàn lại | User/Operator nhập | FR-PAY-06 |
| `reason` | `TEXT NOT NULL` | Lý do xin hoàn tiền (VD: Lỗi cổng, thu sai giá) | User nhập / Operator ghi chú | FR-INC-05 |
| `evidence_url` | `VARCHAR(255) NULL` | Bằng chứng hình ảnh/văn bản | App upload | FR-INC-05 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'REFUND_REQUESTED'` | `REFUND_REQUESTED`, `REFUND_APPROVED`, `REFUND_SUBMITTED`, `REFUND_COMPLETED`, `REJECTED` | Cập nhật theo flow phê duyệt | §3.5.5; FR-PAY-06 |
| `approved_by` | `UUID NULL` | Logical ID trỏ sang `users.id` của Operator/Owner đã phê duyệt (Audit log) | Gán khi Approve | FR-PAY-06 |
| `handled_by_owner` | `BOOLEAN DEFAULT FALSE` | Flag đánh dấu case này do Owner trực tiếp can thiệp (khi Operator leo thang - escalate) | System gán nếu người duyệt là Owner | FR-PAY-06; FR-INC-05 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ yêu cầu | Database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ duyệt | Application | — |

### 5.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `fk_refund_order` | `FOREIGN KEY (payment_order_id, tenant_id) REFERENCES payment_orders(id, tenant_id) ON DELETE RESTRICT` | Bảo vệ toàn vẹn lịch sử thanh toán |
| `chk_refund_status` | `CHECK (status IN ('REFUND_REQUESTED', 'REFUND_APPROVED', 'REFUND_SUBMITTED', 'REFUND_COMPLETED', 'REJECTED'))` | Lifecycle nghiêm ngặt của refund (§3.5.5) |
| `idx_refund_requests_order_tenant` | `(payment_order_id, tenant_id)` | Tìm các khiếu nại của 1 đơn hàng |

---

## 6. Bảng `provider_webhooks` — Hook nhà cung cấp

### 6.1 Mục đích nghiệp vụ

Bảng này hoạt động như một "hộp thư đến" (Inbox) chứa các callback thô từ VNPay/MoMo. Nguyên tắc hệ thống: khi nhận Webhook, lưu thô vào DB ngay lập tức với status `PENDING`, trả `HTTP 200 OK` cho Provider, sau đó mới dùng background job xử lý payment logic. Việc này chống thất thoát dữ liệu nếu xử lý lỗi (backend validation - §3.5.3).

### 6.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-04** | R/A | Backend validation of provider results rather than trusting frontend |
| **§3.5.3** | — | Real-time webhook updates from payment providers |

### 6.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh log | Tự sinh | — |
| `provider` | `VARCHAR(50) NOT NULL` | Kênh gửi: `MOMO`, `VNPAY`, `ZALOPAY` | Route handler | §3.5.3 |
| `provider_event_id` | `VARCHAR(255) NOT NULL` | ID của sự kiện do đối tác cấp (chống xử lý đúp webhook) | Parse từ payload | FR-PAY-04 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'PENDING'` | `PENDING`, `PROCESSED`, `FAILED` (lỗi logic nội bộ khi xử lý webhook) | Cập nhật sau khi process background | — |
| `payload` | `JSONB NULL` | Raw JSON data từ đối tác | Body parser | §3.5.3 |
| `received_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ nhận | Database | — |

### 6.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_provider_event` | `UNIQUE (provider, provider_event_id)` | **Idempotency Webhook**: Chặn đối tác bắn 1 event 2 lần |
| `chk_webhook_status` | `CHECK (status IN ('PENDING', 'PROCESSED', 'FAILED'))` | Trạng thái xử lý nội bộ của webhook |

---

## 7. Tóm tắt quan hệ giữa các bảng

```text
payment_orders (1) ──── (N) payment_transactions   [Luồng tiền chi tiết]
payment_orders (1) ──── (0..1) invoices            [Xuất PDF kế toán]
payment_orders (1) ──── (0..N) refund_requests     [Khiếu nại / Hoàn tiền]

provider_webhooks (N) ──── (logic) payment_transactions [Webhook báo Txn Success]
```

---

## 8. Tóm tắt Cross-service References

| Cột | Bảng hiện tại | Trỏ đến Service (Bảng tham chiếu) | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id`, `site_id` | Tất cả bảng | Parking Service (`tenants.id`, `parking_sites.id`) | Logical ID, context block |
| `account_id` | `payment_orders` | User Service (`users.id`) | Logical ID, context pass từ Reservation/Gateway |
| `reference_id` | `payment_orders` | Reservation Service (`reservations.id` hoặc `parking_sessions.id` tùy `reference_type`) | Bắt buộc truyền ID gốc để tính tiền và đối soát |
| `session_id` | `invoices` | Reservation Service (`parking_sessions.id`) | Logical ID ghi thẳng vào hóa đơn |
| `approved_by` | `refund_requests` | User Service (`users.id`) | Audit log của nhân sự (Operator/Owner) xử lý hoàn tiền |

---

## 9. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key. |
