# SPARK-340 · Báo cáo Review Độc lập & Phê duyệt Chính thức v1.6 (Final Sign-off)

**Phán quyết: APPROVED — CHÍNH THỨC PHÊ DUYỆT LÀM SINGLE SOURCE OF TRUTH (SSOT)**
**Ngày phê duyệt:** 2026-11-12
**Phiên bản tài liệu:** [admin-platform-policies-and-integrations-spark-340-api-design.md](admin-platform-policies-and-integrations-spark-340-api-design.md) (v1.6)
**Lead Software Architect & Reviewer:** Multi-Agent Task Orchestrator
**Nhiệm vụ liên quan:** SPARK-340 (Parent Epic), SPARK-368 (Backend), SPARK-369 (Frontend), SPARK-370 (QA & UAT)

---

## 1. Tóm tắt Đánh giá & Kết quả Đối soát sau Vòng 6

Bản v1.6 của tài liệu API Design đã giải quyết triệt để 100% các phát hiện (findings) còn tồn đọng từ Báo cáo Review Vòng 6 (v1.5), cụ thể:

| Mã Finding | Nội dung rà soát | Tình trạng trong v1.6 | Đánh giá |
|---|---|---|---|
| **Critical · F1** | **Chữ ký Webhook đúng chưa đủ để ghi nhận thu tiền:** Cần ngăn chặn việc callback ký đúng key nhưng mang trạng thái giao dịch thất bại, sai số tiền hoặc là bản gửi lặp làm đổi trạng thái order hay kích hoạt booking confirmation. | **ĐÃ GIẢI QUYẾT TRIỆT ĐỂ (§5.8, Dòng 1208–1221):** Quy định rõ quy trình: Sau khi chữ ký hợp lệ theo đúng key version của order, Payment kiểm tra `status == SUCCESS`, kiểm tra khớp chính xác số tiền (`AMOUNT_MISMATCH`), xử lý idempotent cho cùng collection ID, ghi nhận outbox độc lập cho từng collection riêng biệt. Bổ sung ma trận 6 ca nghiệm thu QA chi tiết. | **PASS (CLOSED)** |
| **Major · F2** | **Hold còn hạn vẫn có thể mất hiệu lực nếu claim bị vô hiệu hóa:** Không được tự ý vào fast path confirm nếu claim không còn được bảo vệ, kể cả khi `evaluatedAt < H`. | **ĐÃ GIẢI QUYẾT TRIỆT ĐỂ (§6.1, Dòng 1370–1380):** Dẫn đúng luồng xác nhận guarded của Operations theo quyết định D11-C/D/E. Giữ nguyên kiểm tra Facility coordination. Trường hợp claim bị vô hiệu hóa trước $H$, bắt buộc tái thẩm định và giành lại claim tương thích dưới khóa trước $T_{start}$; nếu thất bại, giữ khoản thu và chuyển sang đối soát, không cấp quyền đỗ. | **PASS (CLOSED)** |
| **Minor · M-A** | Đồng bộ SQL Sequence 1 với hàm tăng token cập nhật bản thảo | **ĐÃ GIẢI QUYẾT (Dòng 1497):** Sử dụng `GREATEST(CURRENT_TIMESTAMP, @expectedUpdatedAt + interval '1 microsecond')` đồng nhất với đặc tả chính tại Dòng 901. | **PASS (CLOSED)** |
| **Minor · M-B** | Đồng bộ envelope của receipt kiểm tra điều khoản hết hạn | **ĐÃ GIẢI QUYẾT (Dòng 1397–1408):** Đóng gói DTO trong `{ "success": true, "data": { "state": "REJECTED", "code": "TERMS_CHANGED_OR_EXPIRED", "reservationId": null } }` khớp 100% với `ReservationRequestsController`. | **PASS (CLOSED)** |
| **Minor · M-C** | Điều kiện loại bỏ retired key trong Keyring | **ĐÃ GIẢI QUYẾT (Dòng 141–143):** Xác định rõ điều kiện đồng thời: Đã qua 24 giờ kể từ `retired_at` VÀ toàn bộ in-flight checkout dùng key đó đã kết thúc. | **PASS (CLOSED)** |

---

## 2. Đánh giá Tính Sẵn sàng Triển khai (Readiness for Implementation)

### 2.1 Backend (SPARK-368) — READY
- **9 Endpoints** được định nghĩa đầy đủ method, route, input/output JSON schemas, HTTP status codes, error codes:
  1. `GET /api/v1/admin/policies/operations/catalogue` (14 required operational policy keys).
  2. `GET /api/v1/admin/policies/operations/effective` (Platform default -> Site override -> Context).
  3. `GET /api/v1/admin/policies/operations/revisions` (Audit trail, pagination).
  4. `POST /api/v1/admin/policies/operations/draft` (Platform draft creation with bounds validation).
  5. `PUT /api/v1/admin/policies/operations/draft/{id}` (Optimistic concurrency with token increment & scope priority check).
  6. `POST /api/v1/admin/policies/operations/revisions/{id}/activate` (GiST exclusion check, contiguous intervals).
  7. `GET /api/v1/admin/integrations/payment` (PayOS status with masked secrets).
  8. `PUT /api/v1/admin/integrations/payment` (Mode isolation, baseUrl allowlist, secret protection).
  9. `GET /api/v1/admin/integrations/notifications` (Fixture delivery channels status).
- **Tuân thủ Clean Architecture 5 lớp:** Controller $\rightarrow$ MediatR $\rightarrow$ FluentValidation $\rightarrow$ Domain Resolver / Entity $\rightarrow$ Repository Port $\rightarrow$ EF Core / GiST DB.
- **Bảo mật tuyệt đối:** Không rò rỉ secret trên bất kỳ đường dẫn nào; kiểm tra scope trước concurrency (`FORBIDDEN_SCOPE_WRITE`).

### 2.2 Frontend (SPARK-369) — READY
- Hợp đồng dữ liệu request/response đã được chốt và đồng bộ:
  - Envelope chuẩn `{ success: true, data: ... }`.
  - Bảng catalogue hiển thị rõ unit, default value, bounds, và overridable status.
  - Effective policy view có đủ nhãn phân biệt `inherited`, `source`, và cờ `isZeroNotDisabled`.
  - Giao diện Payment integration hiển thị rõ `clientIdMasked`, badges trạng thái (`HEALTHY`, `MISCONFIGURED`, `UNTESTED`), và danh sách `baseUrl` allowlist theo mode.

### 2.3 QA & Test Automation (SPARK-370) — READY
- Danh mục mã lỗi chi tiết giúp QA viết assert chính xác: `POLICY_BOUNDS_VIOLATED`, `OVERLAPPING_POLICY_INTERVAL`, `POLICY_NOT_FOUND`, `FORBIDDEN_SCOPE_WRITE`, `INVALID_PAYMENT_BASE_URL`, `INVALID_WEBHOOK_SIGNATURE`, `AMOUNT_MISMATCH`.
- Kịch bản UAT và integration test được đặc tả tường minh tại Mục 6 (Snapshot immutability, GiST exclusion, Keyring rotation resilience).

---

## 3. Quyết định & Phê duyệt

1. **Chính thức phê duyệt tài liệu [admin-platform-policies-and-integrations-spark-340-api-design.md](admin-platform-policies-and-integrations-spark-340-api-design.md) (v1.6) chuyển trạng thái thành `APPROVED — IMPLEMENTATION READY`.**
2. **Khóa cứng (Freeze) Hợp đồng Kỹ thuật v1.6 làm Single Source of Truth.**
3. **Bật đèn xanh cho Phase 2 (Backend SPARK-368), Phase 3 (Frontend SPARK-369) và Phase 4 (QA SPARK-370) tiến hành triển khai ngay lập tức.**
