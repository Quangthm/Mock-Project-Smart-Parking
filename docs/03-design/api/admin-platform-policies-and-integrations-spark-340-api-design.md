# Admin Platform Policy Bounds & Authorized Integration Settings — API Design & Architecture Contract

| Metadata | Value |
| --- | --- |
| Status | APPROVED — IMPLEMENTATION READY · Đã phê duyệt chính thức sau Review vòng 6 |
| Contract version / date | v1.6 / 2026-11-12 |
| Owning services / authors | `SmartParking.OperationService` (Operational Policies)<br/>`SmartParking.PaymentService` (Payment Gateway Integration)<br/>`SmartParking.NotificationAuditService` (Delivery Fixtures)<br/>*Lead Software Architect & Technical Specification Specialist* |
| Jira design / tasks | Parent Epic: [SPARK-340](https://smartpark.atlassian.net/browse/SPARK-340) [US-A02]<br/>Child Tasks: [SPARK-368](https://smartpark.atlassian.net/browse/SPARK-368) (Backend), [SPARK-369](https://smartpark.atlassian.net/browse/SPARK-369) (Frontend), [SPARK-370](https://smartpark.atlassian.net/browse/SPARK-370) (QA & UAT) |
| Requirements baseline | SRS v1.1 ([FR-LOT-06](../../01-requirements/srs/modules/02-facility-capacity.md#fr-lot-06), [FR-POL-01](../../01-requirements/srs/modules/04-reservations-allocation.md#fr-pol-01) – [FR-POL-08](../../01-requirements/srs/modules/04-reservations-allocation.md#fr-pol-08), [UC-POL-01](../../01-requirements/use-cases/uc-pol-01.md), [UC-POL-02](../../01-requirements/use-cases/uc-pol-02.md), [Policy Catalogue v1.1](../../01-requirements/business-rules/policy-catalogue.md)) |
| Architecture baseline | [SmartPark 5-Layer Clean Architecture](../../07-skills/SmartPark-Coding-Skill-v1.0.md), [Authorization Freshness ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md), [Authorization Operation Matrix](../../01-requirements/srs/authorization-operation-classification.md), [Permissions](../../01-requirements/srs/permissions.md) |
| Implementation target | `SmartParking.OperationService.API`, `SmartParking.PaymentService.API`, `SmartParking.NotificationAuditService.API`, `FE/src/roles/admin/` |

---

## Amendment P1-R1 — PayOS-only MVP deferral

**Release amendment, 25/11/2026:** P1-R1 design and CP1/CP2 source review were ACCEPTED by the Tech Lead. The v1.6 approval and specifications below remain historical design evidence; this amendment governs Payment Admin availability in the scoped MVP. Verification results are reported separately, not implied by design acceptance.

| Surface | Scoped MVP contract |
| --- | --- |
| `GET/PUT /api/v1/admin/integrations/payment` | Deferred. `[NonController]` excludes `AdminPaymentIntegrationController` from default MVC controller/action/endpoint/ApiExplorer discovery. Normal requests, including valid Admin JWTs and malformed PUT bodies, receive route-unavailable **404 with an empty body and no Content-Type**, rather than the JSON error envelope described for active endpoints below. No Admin query/command dispatch, settings read/write, version/history allocation or PayOS HTTP is caused by these requests. |
| Admin PayOS UI | Static **Payment configuration deferred** / **PayOS configuration is unavailable in this MVP.** No Payment GET/PUT, credential/mode/BaseUrl form, Save, Retry or fabricated health status. |
| Notification inspection | `GET /api/v1/admin/integrations/notifications` and its read-only card remain active with existing authentication, loading/error/retry/channel ordering. Notification delivery/auth/audit are retained. |
| Policy and Payment runtime | All Policy APIs, F8, Owner/Admin invariants, accepted snapshots, Payment settings/history/hydration, checkout/webhook/query/cancel, redaction/signatures/idempotency, signing bindings/table and DataProtection keys remain retained. |

The 404 statement describes the normal pipeline, not a bypass of middleware: an allowed CORS preflight may return 204; `X-Discovery-Service-Key` sent to a non-discovery path is rejected by the existing discovery middleware with 403 `SERVICE_SCOPE_DENIED`; authentication/JWKS and transport failures retain their own behavior. Do not disable shared authentication or add global error middleware to force a uniform status/body. No feature flag or ON profile is introduced. Re-enablement needs a separately reviewed source/release change.

The collection inventory changes from 58 historical requests to **48 retained requests plus 2 Admin GET/PUT lock negatives = 50 active requests**. Ten historical Payment positive/RBAC cases are **DEFERRED**, not PASS/SKIP. The two new requests verify HTTP availability only; isolated TestServer counters establish the no-settings/history/provider-effects guarantee. Notification, Operation, mixed RBAC and four F8 cases remain unchanged.

Secret protection and least privilege remain mandatory for retained code. Recipient routing, the legacy MISSING_BINDING reconciliation, U19/SPARK-273 and tester acceptance remain separate dependencies. Deferral does not establish real multi-Owner recipient correctness or real-money readiness.

## 1. Overview & Architectural Context

Tài liệu này là **Hợp đồng Kỹ thuật Duy nhất (Single Source of Truth - SSOT)** cho Epic **SPARK-340 [US-A02]**, quy định chi tiết 9 endpoints giao diện lập trình ứng dụng (API Design & Contract) hoàn chỉnh cho 3 nhóm tính năng:
1. **Quản trị Danh mục Chính sách Vận hành (Operational Policy Management):** Xem danh mục chính sách chuẩn gồm 14 tham số của `ReservationPolicyResolver.RequiredKeys`, giải quyết chính sách hiệu lực (Effective Policy Resolution Engine), quản lý lịch sử phiên bản (Revisions), tạo bản thảo (Create Draft), cập nhật bản thảo an toàn với kiểm tra quyền ưu tiên và khóa lạc quan (Edit Draft with Priority Scope Check & Optimistic Concurrency) và kích hoạt/lên lịch kích hoạt nguyên tử (Atomic Scheduled Activation) được bảo vệ bởi PostgreSQL GiST exclusion constraint.
2. **Quản trị Tích hợp Cổng Thanh toán PayOS (Payment Integration Management):** Quản lý chế độ môi trường (Live / Sandbox / Fixture), cô lập hoàn toàn credentials theo từng mode (Mode-Isolated Credentials), bảo vệ đích kết nối bằng danh sách cho phép (BaseUrl Allowlist), che giấu bí mật tuyệt đối (Secret Masking / Redaction), bảo vệ in-flight checkouts khi rotate key và cấm ghi nhật ký raw credentials trên mọi đường thành công lẫn lỗi.
3. **Giám sát Trạng thái Delivery Adapters (Notification Channel Fixtures):** Kiểm tra trạng thái sẵn sàng và chế độ của 4 kênh thông báo (Email, SMS, Push, In-App).

### 1.1 Nguyên lý Kiến trúc Bất biến (Non-Negotiable Architectural Invariants)

1. **Ranh giới Miền Dịch vụ & Trách nhiệm Tầng (Service Boundaries & Clean Architecture 5 Layers):**
   - `OperationService` sở hữu toàn bộ bảng `operational_policies`, domain resolver `ReservationPolicyResolver` (nằm trong `ReservationPolicies.cs`) và các endpoints quản trị chính sách vận hành (`/api/v1/admin/policies/operations/...`).
   - `PaymentService` sở hữu cấu hình adapter PayOS, bảng biểu phí `policy_revisions` (trong PaymentDb) và endpoints tích hợp thanh toán (`/api/v1/admin/integrations/payment`).
   - `NotificationAuditService` sở hữu các channel delivery adapters và endpoints trạng thái thông báo (`/api/v1/admin/integrations/notifications`).
   - Tầng Application điều phối logic qua Interface Repository Port (`IReservationPolicyRepository`); tầng Persistence thực hiện truy vấn EF Core và xử lý GiST exclusion; Domain thuần túy đánh giá điều kiện kinh doanh (`ReservationPolicyResolver.Resolve`). Không gọi DbContext/SQL trực tiếp từ Controller hay Application Handler.
2. **Giới hạn Quyền Hạn Admin theo Phạm vi & Ưu tiên Kiểm tra Quyền (Scope Authorization — C1 Closed):**
   - Ma trận quyền `policy.operational.publish` ([permissions.md](../../01-requirements/srs/permissions.md)) chỉ cấp cho Quản trị viên (Admin) quyền ghi các giá trị mặc định và biên giới hạn của nền tảng (**Platform Scope**: `tenant_id IS NULL AND site_id IS NULL`).
   - **Admin không có quyền ghi lựa chọn của bãi (Site Scope):** Admin KHÔNG ĐƯỢC tạo draft, sửa draft, hoặc kích hoạt revision cho một bãi xe cụ thể (`tenant_id != null, site_id != null`), cũng như không được quyết định cờ `ArrivalLateToleranceEnabled` của bãi (thuộc độc quyền của Lot Owner). Với platform draft create/update, trường này phải omitted hoặc `null`; cả `true` và `false` bị từ chối bởi validation với lỗi field, không còn được nhận rồi bỏ qua (F8,17/11/2026).
   - Quyền của Admin đối với Site Scope chỉ là **READ-ONLY**: Admin được phép gọi `GET /effective?tenantId=...&siteId=...` để xem chính sách hiệu lực phục vụ hỗ trợ kỹ thuật và đối soát.
   - **Quy tắc Ưu tiên Kiểm tra Scope (Priority Scope Evaluation):**
     Khi Admin gọi `PUT /draft/{id}` hoặc `POST /revisions/{id}/activate`, hệ thống kiểm tra scope của bản ghi mục tiêu `{id}` trước tiên:
     Nếu bản ghi mục tiêu có `tenant_id != null || site_id != null` $\rightarrow$ hệ thống **ngay lập tức từ chối với `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`)**, bất kể `expectedUpdatedAt` gửi lên có khớp hay không, và không bao giờ thực thi câu lệnh SQL UPDATE xuống dòng của Site Scope.
   - Thao tác cấu hình nền tảng **không cấp quyền** thao tác thiết bị bãi xe (barie, camera) hay quyền can thiệp vào các bãi xe cụ thể ([FR-LOT-06](../../01-requirements/srs/modules/02-facility-capacity.md#fr-lot-06)).
   - Áp dụng phân loại độ tươi `TOKEN` theo chuẩn phê duyệt ngày 2026-11-04 ([Authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md)) với claim `role=ADMIN`.
3. **Cô lập Credentials theo Mode & Bảo vệ Đích Kết nối (Mode-Isolated Credentials & BaseUrl Allowlist — C2 Closed):**
   - **Cô lập Credential theo Mode (Mode Isolation):**
     - `LiveCredentials`: Lưu credentials môi trường Production thật (`ClientId`, `ApiKey`, `ChecksumKey`). Chỉ được sử dụng khi `mode == "LIVE"`.
     - `SandboxCredentials`: Lưu credentials môi trường Test của PayOS. Chỉ được sử dụng khi `mode == "SANDBOX"`.
     - `FIXTURE` Mode: **Chặn 100% cuộc gọi ra mạng (Zero Outbound HTTP Calls)**. Khi ở chế độ FIXTURE, adapter tự động giả lập link thanh toán cục bộ (`http://localhost:5173/payment/success?...`) và mã QR mock trong bộ nhớ mà **hoàn toàn không đính kèm Live/Sandbox API keys vào bất kỳ request mạng nào**. Chuyển sang FIXTURE đảm bảo phát sinh đúng 0 HTTP request mang credentials ra ngoài.
   - **Bảo vệ Đích Kết nối (BaseUrl Allowlist):**
     - `baseUrl` gửi lên trong `PUT` bắt buộc phải thuộc Allowlist cố định theo `mode`:
       - `LIVE`: `"https://api-merchant.payos.vn"`
       - `SANDBOX`: `"https://api-merchant.payos.vn"`
       - `FIXTURE`: `"http://localhost:5173"` hoặc `"https://fixture.internal"`
     - Server từ chối mọi URL không nằm trong Allowlist tương ứng với mã `400 Bad Request` (`INVALID_PAYMENT_BASE_URL`).
     - `HttpClientHandler` cấu hình cấm chuyển hướng tự động qua origin khác (`AllowAutoRedirect = false`).
     - Khi provider trả về lỗi HTTP, adapter không ghi nguyên raw response body nếu có nguy cơ phản chiếu credentials.
4. **Ngữ nghĩa 3 Trạng thái (Semantics: Unset/Null vs Zero vs Disabled):**
   - `Unset / Null`: Kế thừa giá trị mặc định của Platform.
   - `Zero (0)`: Là một giá trị định lượng số học hợp lệ (ví dụ: `INTER_BOOKING_BUFFER = 0` phút có nghĩa là không cần khoảng đệm giữa 2 booking, không phải bị disabled; trả về cờ `isZeroNotDisabled: true`).
   - `Disabled`: Trạng thái tắt tính năng một cách tường minh qua cờ boolean riêng biệt (ví dụ: `ArrivalLateToleranceEnabled = false`).
5. **Tính Bất biến của Snapshot Lịch sử (Snapshot Immutability — M5 Closed):**
   - Khi tài xế xem trước điều khoản (`POST /api/reservation-terms`), `OperationService` lưu điều khoản vào bảng `reservation_terms.snapshot_json`.
   - Khi đặt chỗ được xác nhận, snapshot được đóng băng vĩnh viễn vào bảng `reservations` tại 2 cột: `accepted_operational_policy_snapshot` (từ OperationService) và `accepted_pricing_snapshot` (từ PaymentService).
   - Bất kỳ thay đổi chính sách nào của Admin đều chỉ tạo revision mới và áp dụng cho các giao dịch trong tương lai (`effective_from`). **Không bao giờ hồi tố (retroactive repricing/re-policing)**.
6. **Kiểm soát Xung đột Khoảng Thời gian (GiST Exclusion & Contiguous Intervals — M3 Closed):**
   - Bảng `operational_policies` sử dụng GiST exclusion constraint trên khoảng nửa mở: `tstzrange(effective_from, effective_until, '[)')`.
   - Hai khoảng kề nhau $[t_1, t_2)$ và $[t_2, t_3)$ không bao giờ xung đột trong PostgreSQL GiST (`&&` trả về false).
   - Khi kích hoạt revision mới tại $T$, hệ thống khóa target draft và các active rows trong transaction, tìm bản ghi tiền nhiệm đang phủ $T$ ($effective\_from < T < effective\_until$), cập nhật tiền nhiệm kết thúc tại $T$, và gán revision mới bắt đầu tại $T$. Nếu có revision kế tiếp $S$ bắt đầu tại $T_{next} > T$, revision mới kết thúc tại $T_{next}$.
   - Nếu xảy ra xung đột đồng thời do 2 admin kích hoạt cùng thời điểm hoặc cùng kích hoạt một draft, GiST constraint và row lock chặn với mã lỗi tương ứng `409 OVERLAPPING_POLICY_INTERVAL` hoặc `422 CANNOT_ACTIVATE_NON_DRAFT`.
7. **Bảo vệ Chính sách Chống Lạm dụng (Anti-Abuse Platform Protection):**
   - Các khoá `RAPID_CANCELLATION_*` và `DAILY_CANCELLATION_LIMIT` là thuộc tính toàn hệ thống (Platform-only), Owner **không bao giờ được phép override**; bảng `operational_policies` có CHECK constraint chặn các cột này ở Site Scope.

---

## 2. Traceability Matrix

| Mã yêu cầu | Phân loại | Nội dung nghiệp vụ | Mục hiện thực trong tài liệu |
|---|---|---|---|
| **FR-POL-01** / **FR-POL-02** | Functional | Kế thừa chính sách: Platform Default/Bounds $\rightarrow$ Site Override $\rightarrow$ Context. Phân biệt rõ Null (kế thừa), 0 (giá trị hợp lệ) và Disabled. | `GET /api/v1/admin/policies/operations/effective` (§5.2) |
| **FR-POL-03** | Functional | Cung cấp danh mục chính sách vận hành chuẩn có kiểu dữ liệu, đơn vị, giá trị mặc định và biên giới hạn. Áp dụng cho 14 keys của `ReservationPolicyResolver.RequiredKeys`. | `GET /api/v1/admin/policies/operations/catalogue` (§5.1) |
| **FR-POL-04** | Functional | Kiểm tra tính hợp lệ của tham số: `minDuration <= maxDuration`, kiểm tra tính hợp lệ của bounds (chỉ cho phép 10 keys overridable, tính khả dụng theo đơn vị giờ/ngày, late tolerance trong 15..30), không trùng lấn khoảng thời gian kích hoạt. | `POST /draft`, `PUT /draft/{id}`, `POST .../activate` (§5.4, §5.5, §5.6) |
| **FR-POL-05** | Functional | Lưu trữ lịch sử phiên bản chính sách; chính sách mới chỉ áp dụng cho tương lai, không hồi tố. | `GET /revisions` (§5.3), Snapshot Immutability (§6.1) |
| **FR-POL-06** | Functional | Hiển thị chính sách hiệu lực, giá cước và điều khoản trước khi khách xác nhận; chấp nhận thay đổi yêu cầu xem lại điều khoản mới. Khi chính sách đổi giữa chừng lúc review và confirm, yêu cầu tái xác nhận. | Ràng buộc phụ thuộc luồng Driver; Snapshot freeze (§6.1) |
| **FR-POL-07** | Functional | Bảo vệ chính sách nền tảng; cấm Owner ghi đè các tham số chống lạm dụng (`RAPID_CANCELLATION_*`). | `ReservationPolicyResolver`, `POST/PUT draft` validation (§5.4, §5.5) |
| **FR-POL-08** | Functional | Hỗ trợ tạo bản thảo chính sách (draft), sửa bản thảo với khóa lạc quan và lên lịch kích hoạt trong tương lai (`effective_from`). | `POST /draft`, `PUT /draft/{id}`, `POST .../activate` (§5.4, §5.5, §5.6) |
| **FR-LOT-06** | Functional | Quản trị cấu hình tích hợp cổng thanh toán và delivery adapters; che giấu secret; không cấp quyền can thiệp thiết bị bãi. | `GET/PUT /api/v1/admin/integrations/payment`, `GET /notifications` (§5.7, §5.8, §5.9) |
| **UC-POL-01** | Use Case | Cấu hình chính sách nền tảng và quản trị biên giới hạn. | Nhóm 1 (Endpoints 1 – 6) |
| **UC-POL-02** | Use Case | Quản lý tích hợp hệ thống (Payment, Notification Fixtures). | Nhóm 2 & 3 (Endpoints 7 – 9) |
| **BR-POL-01** | Business Rule | Khung giải quyết chính sách hiệu lực và tính bất biến của snapshot. | Mục 1.1 Invariant 4, 5 & Mục 6.1 |
| **BR-POL-02** | Business Rule | Kiểm tra tính nhất quán và loại trừ xung đột thời gian (GiST exclusion). | Mục 1.1 Invariant 6 & Mục 6.2 |
| **BR-AUTH-01** | Business Rule | Phân quyền Admin dựa trên JWT Bearer với model độ tươi `TOKEN`. | Mục 3 & Endpoint Specifications |

---

## 3. Security, Secret Redaction & Outbound Allowlist

### 3.1 Phân quyền & Giới hạn Resource Scope (C1 Closed)
- **Authentication Scheme:** `Bearer <JWT_TOKEN>` do `UserService` cấp.
- **Claims Bắt buộc:** `role = "ADMIN"`, `sub` = Admin ID.
- **Freshness Classification:** `TOKEN` (kiểm tra cục bộ chữ ký, thời hạn sống 15 phút, chấp nhận độ trễ trong vòng đời token).
- **Ràng buộc Phạm vi Ghi & Thứ tự Đánh giá Quyền (Scope Priority Check):**
  - Trong `PUT /draft/{id}` và `POST /revisions/{id}/activate`:
    Tầng Application truy vấn kiểm tra bản ghi mục tiêu:
    ```csharp
    var target = await _repository.GetRevisionMetadataByIdAsync(id, cancellationToken);
    if (target == null)
        throw new NotFoundException("Policy revision not found.");

    // Ưu tiên kiểm tra quyền Scope trước mọi kiểm tra concurrency hay payload
    if (target.TenantId != null || target.SiteId != null)
        throw new ForbiddenScopeException("Admin is only authorized to modify or activate Platform-scoped policies.");
    ```
  - Nếu Admin gửi ID của một Site draft, request **luôn luôn trả về `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`)**, không bao giờ lọt xuống DB UPDATE.

### 3.2 Quy tắc Che giấu Bí mật & Cô lập Credentials theo Mode (C2 Closed)
1. **Response Masking Algorithm:**
   - Trường `ApiKey` và `ChecksumKey` không bao giờ xuất hiện trong response DTO; thay vào đó là boolean flags: `apiKeyConfigured`, `checksumKeyConfigured`.
   - Trường `clientIdMasked`:
     - Nếu chuỗi rỗng hoặc null: trả về `null`.
     - Nếu độ dài $L > 4$: trả về `****` ghép với 4 ký tự cuối (ví dụ: `"client_abc1234"` $\rightarrow$ `"****1234"`).
     - Nếu độ dài $L \le 4$: trả về `"****"`.
2. **Safe PUT Semantics & Mode Isolation:**
   - Cấu hình PayOS lưu trữ tách biệt theo từng mode: `LiveCredentials` và `SandboxCredentials`.
   - Khi Admin đổi `mode`:
     - Nếu chuyển từ `LIVE` sang `SANDBOX`: Hệ thống nạp `SandboxCredentials`. Nếu chưa cấu hình Sandbox keys, hệ thống báo `isConfigured: false` và `status: "MISCONFIGURED"`, **tuyệt đối không sao chép Live keys sang Sandbox**.
     - Nếu chuyển sang `FIXTURE`: Hệ thống chuyển sang cơ chế giả lập cục bộ, **chặn hoàn toàn việc phát sinh HTTP request mạng**. Bất kỳ Live keys nào đang lưu cũng không bao giờ bị gửi ra ngoài.
   - Nếu trường `apiKey` hoặc `checksumKey` bị bỏ qua (omitted) hoặc là `null`, hệ thống giữ nguyên giá trị đã lưu của mode tương ứng. Nếu client truyền chuỗi rỗng `""`, hệ thống từ chối với lỗi `400 VALIDATION_FAILED` (không cho phép xóa trắng key thành chuỗi rỗng).

### 3.3 Danh sách URL Đích Được Phép (BaseUrl Allowlist — C2 Closed)
Để bảo vệ chống tấn công SSRF và ngăn ngừa việc gửi credentials sang máy chủ bên ngoài không tin cậy, `PaymentService` áp dụng danh sách cho phép (Allowlist):

```csharp
public static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedBaseUrls =
    new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["LIVE"] = new() { "https://api-merchant.payos.vn" },
        ["SANDBOX"] = new() { "https://api-merchant.payos.vn" },
        ["FIXTURE"] = new() { "http://localhost:5173", "https://fixture.internal" }
    };
```
- Khi nhận `PUT`, server xác thực `AllowedBaseUrls[mode].Contains(request.BaseUrl)`. Nếu không khớp, trả về `400 Bad Request` (`INVALID_PAYMENT_BASE_URL`).
- `HttpClientHandler` cấu hình cấm chuyển hướng tự động qua origin khác (`AllowAutoRedirect = false`).
- Khi provider trả về lỗi HTTP, adapter không ghi nguyên raw response body nếu có nguy cơ phản chiếu credentials.

### 3.4 In-Flight Checkouts & Keyring Rotation Resilience (M6 Closed)
- Trong trường hợp Admin cập nhật hoặc xoay vòng `ChecksumKey` (`PUT /api/v1/admin/integrations/payment`), các đơn thanh toán đang chờ khách quét QR (in-flight checkouts) có thể gửi webhook callback về sau đó.
- **Bảo toàn Khóa Ký theo Keyring (Active Keyring Retention):** Thay vì chỉ giữ một khóa liền trước trong thời gian ngắn (có nguy cơ mất khóa nếu Admin xoay vòng nhiều lần liên tiếp $K_0 \rightarrow K_1 \rightarrow K_2$), `PaymentService` quản lý một danh mục Active Keyring:
  - Payment chỉ được xóa key đã nghỉ khi **đồng thời** đã qua 24 giờ kể từ `retired_at` và mọi checkout dùng key đó đã hết hạn hoặc hoàn tất. Nếu callback đến sau khi key đã xóa, giữ nguyên trạng thái order và chuyển chứng cứ sang đối soát với provider. Chỉ ghi nhận khoản thu khi có bằng chứng độc lập đã xác minh.
  - Bảng `payment_orders` ghi nhận trường `signing_key_version` và `mode` tại thời điểm tạo checkout.
- **Ràng Buộc Xác Thực Webhook Chặt Chẽ theo Order Binding (Order-Key Binding Invariant):**
  - Khi webhook IPN gửi về kèm `orderCode`:
    1. **Tìm kiếm Đơn hàng:** Tra cứu `payment_order` theo `orderCode`. Nếu không tìm thấy $\rightarrow$ Trả về ngay `404 Not Found` (`ORDER_NOT_FOUND`). Tuyệt đối không tự gán thanh toán hay phát sinh sự kiện xác nhận cho bất kỳ đặt chỗ nào.
    2. **Xác thực Độc quyền theo Phiên bản Khóa của Đơn hàng:** Adapter trích xuất đúng `signing_key_version` và `mode` của đơn hàng đó, lấy `ChecksumKey` tương ứng trong Keyring để xác thực chữ ký HMAC-SHA256.
    3. Chữ ký sai trả `400 INVALID_WEBHOOK_SIGNATURE` và giữ nguyên trạng thái hiện tại của order. Chữ ký đúng cho phép Payment tiếp tục kiểm tra kết quả giao dịch, số tiền và định danh khoản thu theo §5.8.
- Payment ghi nhận từng khoản thu đã xác minh. Operations quyết định quyền giữ chỗ theo thời điểm và tình trạng claim tại checkpoint cuối (§6.1). [Quyết định D11-B đến D11-E](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md)

### 3.5 Nhật ký Kiểm toán An toàn (Redacted Audit Trail)
Ghi log kiểm toán cho mỗi lần thay đổi cấu hình mà không bao giờ in credentials, đồng thời ghi nhận scope và phiên bản cấu hình theo chuẩn UC-POL-02:
```json
{
  "event": "PAYMENT_INTEGRATION_UPDATED",
  "scope": "PLATFORM",
  "configVersion": 3,
  "actorId": "11111111-1111-1111-1111-111111111111",
  "provider": "PAYOS",
  "mode": "SANDBOX",
  "baseUrl": "https://api-merchant.payos.vn",
  "keysModified": ["ApiKey", "ChecksumKey"],
  "timestamp": "2026-11-12T10:20:00Z",
  "remoteIp": "10.0.0.1"
}
```

---

## 4. Error Code Catalogue

Toàn bộ các endpoints tuân thủ cấu trúc lỗi chuẩn của SmartPark:

```json
{
  "success": false,
  "code": "VALIDATION_FAILED",
  "message": "The request payload failed one or more validation rules.",
  "traceId": "9b1deb4d3b7d4bad9bdd2b0d7b3dcb6d",
  "errors": [
    {
      "property": "minReservationDurationMinutes",
      "message": "Minimum reservation duration must be greater than zero."
    }
  ]
}
```

| HTTP Status | Mã lỗi máy (Code) | Nguyên nhân kích hoạt | Khắc phục phía Client |
|---|---|---|---|
| **400 Bad Request** | `INVALID_REQUEST` | Cú pháp JSON sai, thiếu headers bắt buộc, hoặc chỉ truyền một trong hai tham số `tenantId`/`siteId` khi gọi effective view. | Kiểm tra lại cú pháp payload hoặc truyền đủ cặp `tenantId` và `siteId`. |
| **400 Bad Request** | `VALIDATION_FAILED` | Vi phạm các quy tắc FluentValidation (ví dụ: `minDuration <= 0`, `arrivalLateToleranceMinutes` ngoài khoảng 15..30, hoặc thiếu concurrency token `expectedUpdatedAt`). | Xem mảng `errors` và điều chỉnh lại giá trị trường. |
| **400 Bad Request** | `POLICY_BOUNDS_VIOLATED` | Biên giới hạn không hợp lệ: chứa key lạ ngoài 10 overridable keys, chứa key chống lạm dụng, `minimum > maximum`, `ARRIVAL_LATE_TOLERANCE` bound nằm ngoài 15..30, hoặc bounds không chứa giá trị số nguyên khả dụng trong đơn vị lưu trữ của cột tương ứng (không thỏa mãn $\lfloor maximum / 60 \rfloor \ge \lceil minimum / 60 \rceil$ cho giờ hoặc $\lfloor maximum / 1440 \rfloor \ge \max(0, \lceil minimum / 1440 \rceil)$ cho ngày). | Sửa bounds đảm bảo đúng 10 keys cho phép, min $\le$ max và thỏa mãn tính khả dụng số nguyên. |
| **400 Bad Request** | `INVALID_PAYMENT_BASE_URL` | `baseUrl` truyền vào không thuộc Allowlist đã duyệt cho mode hiện tại. | Chỉ sử dụng các URL được hệ thống cho phép theo mode (`LIVE`/`SANDBOX`/`FIXTURE`). |
| **400 Bad Request** | `SITE_TENANT_MISMATCH` | `tenantId` không phải là chủ sở hữu hợp pháp của `siteId`. | Kiểm tra lại cặp mã định danh Tenant và Site. |
| **401 Unauthorized** | `UNAUTHENTICATED` | Thiếu Bearer token, token hết hạn (quá 15 phút), hoặc chữ ký không hợp lệ. | Đăng nhập lại với tài khoản Quản trị viên để lấy token mới. |
| **403 Forbidden** | `FORBIDDEN` | Token hợp lệ nhưng caller không có role `ADMIN`. | Sử dụng đúng tài khoản Quản trị viên nền tảng. |
| **403 Forbidden** | `FORBIDDEN_SCOPE_WRITE` | Admin cố gắng tạo/sửa draft hoặc kích hoạt chính sách cho Site Scope. | Admin chỉ được phép ghi Platform Scope. Site Scope thuộc quyền Lot Owner. |
| **404 Not Found** | `POLICY_NOT_FOUND` | Revision ID không tồn tại, hoặc bãi xe chưa từng công bố revision nào. | Kiểm tra lại UUID của chính sách hoặc công bố revision cho bãi xe. |
| **404 Not Found** | `SITE_NOT_FOUND` | `siteId` không tồn tại trong hệ thống. | Kiểm tra lại mã định danh bãi xe. |
| **409 Conflict** | `OVERLAPPING_POLICY_INTERVAL` | Khoảng thời gian kích hoạt trùng lấn với revision đang active khác của cùng scope (Vi phạm GiST constraint hoặc trùng mốc bắt đầu). | Chọn mốc `effectiveFrom` không trùng lấn hoặc kiểm tra lại lịch kích hoạt. |
| **409 Conflict** | `CANNOT_MODIFY_ACTIVE_POLICY` | Thao tác `PUT /draft/{id}` gửi tới một revision đã được kích hoạt hoặc đã hết hạn. | Chỉ được phép chỉnh sửa các bản ghi ở trạng thái `DRAFT`. |
| **409 Conflict** | `RESOURCE_VERSION_CONFLICT` | Sửa bản thảo thất bại do xung đột phiên bản (Stale update: `expectedUpdatedAt` không khớp giá trị hiện tại trong DB). | Tải lại bản thảo mới nhất trước khi thực hiện lưu lại. |
| **422 Unprocessable** | `CANNOT_ACTIVATE_NON_DRAFT` | Yêu cầu kích hoạt một revision không ở trạng thái `DRAFT` (ví dụ đã được kích hoạt trước đó bởi lệnh song song hoặc đã hết hạn). | Chỉ kích hoạt các bản ghi đang ở trạng thái `DRAFT`. |
| **500 Internal Error** | `INTERNAL_ERROR` | Lỗi máy chủ không mong muốn. | Ghi nhận `traceId` và liên hệ bộ phận DevOps. |
| **503 Unavailable** | `RESERVATION_POLICY_UNAVAILABLE` | Thiếu phiên bản chính sách Platform active hoặc cấu hình không đầy đủ các required keys. | Seed hoặc kích hoạt một phiên bản Platform hợp lệ. |

Sau khi `POST /api/reservation-requests` trả `202 Accepted`, Driver đọc kết quả qua `GET /api/reservation-requests/{requestId}`. Khi terms đã cũ, `data.state` là `REJECTED`, `data.code` là `TERMS_CHANGED_OR_EXPIRED` và `data.reservationId` là `null` (§6.1).

---

## 5. Detailed API Specifications

---

### Group 1: Quản trị Chính sách Vận hành (`SmartParking.OperationService.API`)

---

### 1. `GET /api/v1/admin/policies/operations/catalogue`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `GET /api/v1/admin/policies/operations/catalogue` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` (Chấp nhận độ trễ token tối đa 15 phút, xác thực chữ ký cục bộ) |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Platform-wide operational policy metadata |
| **Content Type** | Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Quy tắc Dữ liệu & Đối chiếu Seed v1 (M1 Closed)
- Bảng trả về đúng 14 keys chuẩn thuộc `ReservationPolicyResolver.RequiredKeys`.
- **Phân biệt rạch ròi:**
  - `isOverridableByOwner`: Khả năng cho phép ghi đè (true với 10 tham số thời gian, false với 4 tham số chống lạm dụng).
  - `bound`: Biên giới hạn ghi đè **hiện có hiệu lực** trong active Platform revision.
  - Trong Seed v1 mặc định (`03b-reservation-policy-defaults.sql`), cột `override_bounds` chỉ thiết lập:
    `{"ARRIVAL_LATE_TOLERANCE": {"minimum": 15, "maximum": 30}}`.
    Do đó, trong Revision 1, duy nhất `ARRIVAL_LATE_TOLERANCE` có `bound: { minimum: 15, maximum: 30 }`, 13 tham số còn lại có `bound: null` (nghĩa là Owner chưa được cấp phép ghi đè cho đến khi Admin tạo và kích hoạt revision mới có bổ sung bounds).

#### Request Sample
```http
GET /api/v1/admin/policies/operations/catalogue HTTP/1.1
Host: operations.smartpark.internal
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
Accept: application/json
```

#### Response Sample (200 OK — Dựa trên Seed v1 Thực tế)

```json
{
  "success": true,
  "data": {
    "revisionId": "27300000-0000-4000-8000-000000000001",
    "version": 1,
    "effectiveFrom": "2026-11-01T00:00:00Z",
    "cancellationTimezone": "Asia/Ho_Chi_Minh",
    "policies": [
      {
        "key": "RESERVATION_MIN_DURATION",
        "name": "Minimum Reservation Duration",
        "description": "Khoảng thời gian đặt chỗ tối thiểu cho phép trong một phiên đặt.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 15,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "RESERVATION_MAX_DURATION",
        "name": "Maximum Reservation Duration",
        "description": "Khoảng thời gian đặt chỗ tối đa cho phép trong một phiên đặt thông thường.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 1440,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "MAX_ADVANCE_BOOKING",
        "name": "Maximum Advance Booking Horizon",
        "description": "Khoảng thời gian tối đa mà tài xế có thể đặt chỗ trước ngày giờ bắt đầu.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 10080,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "INTER_BOOKING_BUFFER",
        "name": "Inter-booking Buffer",
        "description": "Khoảng đệm tối thiểu giữa hai lượt đặt chỗ liên tiếp trên cùng một vị trí. 0 là hợp lệ (cho phép liền kề).",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 0,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "PAYMENT_HOLD_DURATION",
        "name": "Payment Hold Duration",
        "description": "Thời gian giữ chỗ tạm thời chờ người dùng hoàn tất thanh toán tiền cọc.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 5,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "RESERVATION_PROTECTION_WINDOW",
        "name": "Reservation Protection Window",
        "description": "Cửa sổ bảo vệ chỗ trước giờ bắt đầu đặt; chặn các yêu cầu phân bổ khác chiếm chỗ.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 240,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "ALLOCATION_LEAD_TIME",
        "name": "Allocation Lead Time",
        "description": "Thời gian hệ thống thực hiện giải thuật phân bổ slot cụ thể trước giờ bắt đầu.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 60,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "FACILITY_FAILURE_REFUND_LEAD_TIME",
        "name": "Facility Failure Refund Lead Time",
        "description": "Mốc thời gian trước giờ bắt đầu mà nếu bãi xe gặp sự cố, tài xế được quyền nhận hoàn tiền 100%.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 15,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "OCCUPANCY_VALIDITY_TIMESPAN",
        "name": "Occupancy Validity Timespan",
        "description": "Khoảng thời gian hiệu lực tối đa của bằng chứng cảm biến chiếm chỗ trước khi coi là hết hạn.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 240,
        "isOverridableByOwner": true,
        "bound": null
      },
      {
        "key": "ARRIVAL_LATE_TOLERANCE",
        "name": "Arrival Late Tolerance",
        "description": "Thời gian gia hạn đến muộn sau giờ đặt chỗ trước khi bị đánh dấu vắng mặt (No-show).",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 15,
        "isOverridableByOwner": true,
        "bound": {
          "minimum": 15,
          "maximum": 30
        }
      },
      {
        "key": "RAPID_CANCELLATION_COUNT",
        "name": "Rapid Cancellation Threshold Count",
        "description": "Số lượt hủy đặt chỗ liên tiếp trong cửa sổ ngắn để kích hoạt cơ chế giới hạn tài khoản.",
        "dataType": "count",
        "unit": "submissions",
        "defaultValue": 2,
        "isOverridableByOwner": false,
        "bound": null
      },
      {
        "key": "RAPID_CANCELLATION_WINDOW",
        "name": "Rapid Cancellation Window",
        "description": "Cửa sổ thời gian lăn dùng để tính số lượt hủy đặt chỗ liên tiếp.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 15,
        "isOverridableByOwner": false,
        "bound": null
      },
      {
        "key": "RAPID_CANCELLATION_RESTRICTION_DURATION",
        "name": "Rapid Cancellation Restriction Duration",
        "description": "Thời gian tạm khóa quyền tạo đặt chỗ mới của tài khoản sau khi vi phạm hành vi hủy liên tục.",
        "dataType": "duration",
        "unit": "minutes",
        "defaultValue": 180,
        "isOverridableByOwner": false,
        "bound": null
      },
      {
        "key": "DAILY_CANCELLATION_LIMIT",
        "name": "Daily Cancellation Limit",
        "description": "Giới hạn số lượt hủy đặt chỗ tối đa trong một ngày (tính theo múi giờ Asia/Ho_Chi_Minh).",
        "dataType": "count",
        "unit": "submissions",
        "defaultValue": 5,
        "isOverridableByOwner": false,
        "bound": null
      }
    ]
  }
}
```

#### Bảng Mô tả Thuộc tính Response

| Trường | Kiểu / Format | Bắt buộc / Nullable | Ý nghĩa & Ràng buộc |
|---|---|---|---|
| `revisionId` | `UUID` | Bắt buộc | ID của phiên bản chính sách Platform đang được dùng để cung cấp catalogue. |
| `version` | `integer` | Bắt buộc | Số thứ tự phiên bản của Platform Policy Catalogue đang có hiệu lực. |
| `effectiveFrom` | `string (ISO 8601)` | Bắt buộc | Thời điểm phiên bản chính sách bắt đầu có hiệu lực. |
| `cancellationTimezone` | `string` | Bắt buộc | Múi giờ chuẩn hóa để tính giới hạn hủy trong ngày (`"Asia/Ho_Chi_Minh"`). |
| `policies[]` | `array` | Bắt buộc | Danh sách 14 chính sách chuẩn của hệ thống. |
| `policies[].key` | `string` | Bắt buộc | Mã định danh chuẩn trong `ReservationPolicyResolver.RequiredKeys`. |
| `policies[].name` | `string` | Bắt buộc | Tên hiển thị người dùng của chính sách. |
| `policies[].description` | `string` | Bắt buộc | Giải thích mục đích nghiệp vụ của chính sách. |
| `policies[].dataType` | `string (enum)` | Bắt buộc | Kiểu dữ liệu logic: `"duration"` hoặc `"count"`. |
| `policies[].unit` | `string` | Bắt buộc | Đơn vị đo lường: `"minutes"` hoặc `"submissions"`. |
| `policies[].defaultValue`| `integer` | Bắt buộc | Giá trị mặc định cấp Platform. |
| `policies[].isOverridableByOwner` | `boolean` | Bắt buộc | `false` với các chính sách chống lạm dụng; `true` với các chính sách thời gian. |
| `policies[].bound` | `object` | Nullable | Biên giới hạn ghi đè `{ minimum, maximum }`. Trả về `null` nếu revision hiện tại chưa cấp bounds hoặc `isOverridableByOwner = false`. |

---

### 2. `GET /api/v1/admin/policies/operations/effective`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `GET /api/v1/admin/policies/operations/effective` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Platform-wide hoặc Site-specific resolution (Read-Only) |
| **Content Type** | Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Query Parameters

| Tham số | Vị trí | Kiểu | Bắt buộc | Ràng buộc & Ý nghĩa | Ví dụ |
|---|---|---|---|---|---|
| `tenantId` | Query | `UUID` | Tùy chọn | ID của chủ sở hữu bãi xe. Bắt buộc phải đi kèm `siteId`. | `d3b07384-d113-40f4-a080-000000000001` |
| `siteId` | Query | `UUID` | Tùy chọn | ID của bãi xe. Bắt buộc phải đi kèm `tenantId`. | `c4a07384-d113-40f4-a080-000000000001` |
| `at` | Query | `date-time (ISO 8601)` | Tùy chọn | Mốc thời gian đánh giá chính sách. Mặc định là `DateTimeOffset.UtcNow`. | `2026-11-12T10:00:00Z` |

#### Quy tắc Xử lý & Các Trường hợp Biên (M2 Closed)
1. **Trường hợp 1: Platform Scope (`tenantId == null && siteId == null`):**
   - Đọc bản ghi active trong bảng `operational_policies` tại mốc `at` có `tenant_id IS NULL AND site_id IS NULL`.
   - `scope`: `"PLATFORM"`, `siteRevision`: `null`.
   - Mọi items trong `values` có `inherited = false`, `source = "PLATFORM_DEFAULT"`.
   - `arrivalLateTolerance.enabled`: `null` (vì Platform không quyết định bật/tắt, việc này do từng Site quyết định).
2. **Trường hợp 2: Site Scope Đã Có Revision Hợp lệ (`tenantId != null && siteId != null`):**
   - Đọc Platform active revision và Site active revision tại mốc `at`.
   - Gọi Domain resolver: `ReservationPolicyResolver.Resolve(platformRev, siteRev)` (trong `ReservationPolicies.cs`).
   - `scope`: `"SITE"`.
   - Đối với từng key:
     - **Site row có cấu hình tường minh (`siteRow.Column IS NOT NULL`):**
       - Nếu giá trị của Site khác với Platform default (`overridden != value`): Domain Resolver `ReservationPolicyResolver.Resolve` đối chiếu với `platform.OverrideBounds[key]`. Nếu bound là null hoặc giá trị nằm ngoài bound $\rightarrow$ ném ngoại lệ `ReservationPolicyUnavailableException` (`503 RESERVATION_POLICY_UNAVAILABLE`). Nếu nằm trong bound, nhận giá trị của Site, trả về `value: overridden`, `inherited: false`, `source: "SITE_OVERRIDE"`.
       - Nếu giá trị của Site bằng chính xác Platform default (`overridden == value`): Theo quy tắc của `ReservationPolicyResolver` (nằm trong `ReservationPolicies.cs` dòng 57), nhánh kiểm tra bound được bỏ qua (`overridden != value` là `false`), do đó không ném ngoại lệ kể cả khi Platform chưa cấu hình bound (`bound == null`) hoặc với dữ liệu cũ (legacy data). Trên API response, trường này phản ánh trung thực cấu hình của Site với `value: overridden`, `inherited: false`, `source: "SITE_OVERRIDE"`.
     - **Site row không cấu hình (`siteRow.Column IS NULL`):** Kế thừa hoàn toàn từ Platform revision active: `inherited: true`, `source: "PLATFORM_DEFAULT"`.
     - **Xử lý giá trị 0:** Nếu `value == 0` (ví dụ `interBookingBufferMinutes = 0`), gán cờ `isZeroNotDisabled: true`.
   - `arrivalLateTolerance`: lấy theo `siteRev.ArrivalLateToleranceEnabled` (`true` hoặc `false`), `inherited = false`.
3. **Trường hợp 3: Bãi xe chưa từng công bố Revision nào hoặc không có revision active tại mốc `at`:**
   - Theo quy tắc của `ReservationPolicyResolver` trong `ReservationPolicies.cs`: Việc giải quyết chính sách hiệu lực cho bãi xe bắt buộc phải có một phiên bản của bãi xe với lựa chọn bật/tắt late tolerance rõ ràng (`ArrivalLateToleranceEnabled != null`).
   - Nếu bãi xe tồn tại nhưng chưa có revision nào active tại mốc `at` $\rightarrow$ API trả về `404 Not Found` kèm mã `POLICY_NOT_FOUND` và thông điệp: `"No active policy revision found for this site at the specified timestamp. The lot owner must publish an initial policy revision to establish late tolerance enablement."`.
4. **Trường hợp 4: Chỉ truyền một trong hai tham số `tenantId` hoặc `siteId`:**
   - Trả về `400 Bad Request` kèm mã `INVALID_REQUEST` và thông điệp: `"Both tenantId and siteId must be supplied together for site scope resolution, or both omitted for platform scope resolution."`.
5. **Trường hợp 5: `tenantId` và `siteId` không khớp quyền sở hữu:**
   - Tầng Application kiểm tra tính hợp lệ của cặp ID qua Facility domain context. Nếu không khớp $\rightarrow$ trả về `400 Bad Request` kèm mã `SITE_TENANT_MISMATCH`.

#### Request Sample
```http
GET /api/v1/admin/policies/operations/effective?tenantId=d3b07384-d113-40f4-a080-000000000001&siteId=c4a07384-d113-40f4-a080-000000000001 HTTP/1.1
Host: operations.smartpark.internal
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
Accept: application/json
```

#### Response Sample (200 OK — Site Scope giải quyết với Platform Revision 1 và Site Revision 1)

```json
{
  "success": true,
  "data": {
    "scope": "SITE",
    "tenantId": "d3b07384-d113-40f4-a080-000000000001",
    "siteId": "c4a07384-d113-40f4-a080-000000000001",
    "evaluatedAt": "2026-11-12T10:00:00Z",
    "platformRevision": {
      "id": "27300000-0000-4000-8000-000000000001",
      "version": 1
    },
    "siteRevision": {
      "id": "27300000-0000-4000-8000-000000000002",
      "version": 1
    },
    "values": {
      "RESERVATION_MIN_DURATION": {
        "value": 15,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "RESERVATION_MAX_DURATION": {
        "value": 1440,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "MAX_ADVANCE_BOOKING": {
        "value": 10080,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "INTER_BOOKING_BUFFER": {
        "value": 0,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": true
      },
      "PAYMENT_HOLD_DURATION": {
        "value": 5,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "RESERVATION_PROTECTION_WINDOW": {
        "value": 240,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "ALLOCATION_LEAD_TIME": {
        "value": 60,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "FACILITY_FAILURE_REFUND_LEAD_TIME": {
        "value": 15,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "OCCUPANCY_VALIDITY_TIMESPAN": {
        "value": 240,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "ARRIVAL_LATE_TOLERANCE": {
        "value": 20,
        "unit": "minutes",
        "inherited": false,
        "source": "SITE_OVERRIDE",
        "isZeroNotDisabled": false
      },
      "RAPID_CANCELLATION_COUNT": {
        "value": 2,
        "unit": "submissions",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "RAPID_CANCELLATION_WINDOW": {
        "value": 15,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "RAPID_CANCELLATION_RESTRICTION_DURATION": {
        "value": 180,
        "unit": "minutes",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      },
      "DAILY_CANCELLATION_LIMIT": {
        "value": 5,
        "unit": "submissions",
        "inherited": true,
        "source": "PLATFORM_DEFAULT",
        "isZeroNotDisabled": false
      }
    },
    "arrivalLateTolerance": {
      "enabled": true,
      "minutes": 20,
      "inherited": false
    },
    "cancellationTimezone": "Asia/Ho_Chi_Minh"
  }
}
```

> [!NOTE] QA Fixture: `SiteExplicitEqualDefaultWithNullBoundFixture` (M2 Closed)
> **Mục đích:** Kiểm chứng tính đúng đắn khi bãi xe thiết lập giá trị bằng giá trị mặc định của Platform đối với trường không có bound (bound = null trong Seed v1).
> - Platform Revision: `INTER_BOOKING_BUFFER = 0`, bound: `null`.
> - Site Revision: cấu hình rõ ràng cột `inter_booking_buffer_minutes = 0`.
> - Kết quả khi gọi `GET /api/v1/admin/policies/operations/effective?tenantId=...&siteId=...`: trường `INTER_BOOKING_BUFFER` trong response có giá trị:
>   `{ "value": 0, "inherited": false, "source": "SITE_OVERRIDE", "isZeroNotDisabled": true }`
> - **Kỳ vọng nghiệm thu:** Hệ thống giải quyết thành công (`200 OK`), không ném `ReservationPolicyUnavailableException` (`503`), ghi nhận trung thực nguồn `source: "SITE_OVERRIDE"` và cờ `inherited: false` do giá trị được chỉ định tường minh trên dòng của Site.

---

### 3. `GET /api/v1/admin/policies/operations/revisions`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `GET /api/v1/admin/policies/operations/revisions` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Audit history of policy revisions |
| **Content Type** | Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Query Parameters

| Tham số | Vị trí | Kiểu | Mặc định | Ý nghĩa & Lọc |
|---|---|---|---|---|
| `status` | Query | `string (enum)` | `ALL` | Lọc theo vòng đời: `"ALL"`, `"DRAFT"`, `"SCHEDULED"`, `"CURRENT"`, `"SUPERSEDED"`. |
| `scope` | Query | `string (enum)` | `ALL` | Bộ lọc phạm vi: `"ALL"`, `"PLATFORM"`, `"SITE"`. |
| `tenantId` | Query | `UUID` | `null` | Lọc theo ID của Tenant. |
| `siteId` | Query | `UUID` | `null` | Lọc theo ID của Site. |
| `page` | Query | `integer` | `1` | Số trang (1-based index). Tối thiểu 1. |
| `pageSize` | Query | `integer` | `20` | Kích thước trang. Tối đa 100. |

#### Quy tắc Phân loại Vòng đời & Sắp xếp Ổn định
- **Trạng thái Vòng đời (Computed Lifecycle States):**
  - `"DRAFT"`: `is_active = false`.
  - `"SCHEDULED"`: `is_active = true` và `effective_from > UtcNow`.
  - `"CURRENT"`: `is_active = true` và `effective_from <= UtcNow` và (`effective_until == null || effective_until > UtcNow`).
  - `"SUPERSEDED"`: `effective_until <= UtcNow`.
- **Thứ tự Sắp xếp Ổn định (Deterministic Tie-Breaker):**
  `ORDER BY version DESC, effective_from DESC, id ASC`. Điều này bảo đảm kết quả phân trang nhất quán 100% giữa các trang ngay cả khi có nhiều bản ghi cùng version hoặc cùng timestamp.

#### Request Sample
```http
GET /api/v1/admin/policies/operations/revisions?status=ALL&scope=PLATFORM&page=1&pageSize=20 HTTP/1.1
Host: operations.smartpark.internal
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
Accept: application/json
```

#### Response Sample (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "27300000-0000-4000-8000-000000000099",
        "scope": "PLATFORM",
        "tenantId": null,
        "siteId": null,
        "version": 2,
        "status": "DRAFT",
        "isActive": false,
        "effectiveFrom": "2026-12-01T00:00:00Z",
        "effectiveUntil": null,
        "createdAt": "2026-11-12T10:15:00Z",
        "updatedAt": "2026-11-12T10:15:00Z",
        "paymentHoldDurationMinutes": 5,
        "minReservationDurationMinutes": 15,
        "maxReservationDurationHours": 24,
        "maxAdvanceBookingDays": 7,
        "interBookingBufferMinutes": 0,
        "reservationProtectionWindowHours": 4,
        "allocationLeadTimeMinutes": 60,
        "facilityFailureRefundLeadMinutes": 15,
        "occupancyValidityTimespanHours": 4,
        "arrivalLateToleranceMinutes": 15,
        "arrivalLateToleranceEnabled": null,
        "rapidCancellationCount": 2,
        "rapidCancellationWindowMinutes": 15,
        "rapidCancellationRestrictionMinutes": 180,
        "dailyCancellationLimit": 5,
        "cancellationTimezone": "Asia/Ho_Chi_Minh",
        "overrideBounds": {
          "ARRIVAL_LATE_TOLERANCE": { "minimum": 15, "maximum": 30 },
          "RESERVATION_MIN_DURATION": { "minimum": 15, "maximum": 60 },
          "PAYMENT_HOLD_DURATION": { "minimum": 5, "maximum": 15 }
        }
      },
      {
        "id": "27300000-0000-4000-8000-000000000001",
        "scope": "PLATFORM",
        "tenantId": null,
        "siteId": null,
        "version": 1,
        "status": "CURRENT",
        "isActive": true,
        "effectiveFrom": "2026-11-01T00:00:00Z",
        "effectiveUntil": null,
        "createdAt": "2026-11-01T00:00:00Z",
        "updatedAt": "2026-11-01T00:00:00Z",
        "paymentHoldDurationMinutes": 5,
        "minReservationDurationMinutes": 15,
        "maxReservationDurationHours": 24,
        "maxAdvanceBookingDays": 7,
        "interBookingBufferMinutes": 0,
        "reservationProtectionWindowHours": 4,
        "allocationLeadTimeMinutes": 60,
        "facilityFailureRefundLeadMinutes": 15,
        "occupancyValidityTimespanHours": 4,
        "arrivalLateToleranceMinutes": 15,
        "arrivalLateToleranceEnabled": null,
        "rapidCancellationCount": 2,
        "rapidCancellationWindowMinutes": 15,
        "rapidCancellationRestrictionMinutes": 180,
        "dailyCancellationLimit": 5,
        "cancellationTimezone": "Asia/Ho_Chi_Minh",
        "overrideBounds": {
          "ARRIVAL_LATE_TOLERANCE": { "minimum": 15, "maximum": 30 }
        }
      }
    ],
    "totalCount": 2,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

---

### 4. `POST /api/v1/admin/policies/operations/draft`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `POST /api/v1/admin/policies/operations/draft` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | **Platform Scope Only** (`tenant_id IS NULL AND site_id IS NULL`) |
| **Content Type** | Request & Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Giới hạn Quyền Hạn Admin (C1 Closed)
- Endpoint này chỉ dùng để tạo bản thảo chính sách cấp Nền tảng (**Platform Policy Draft**).
- Request body không chứa `tenantId` hay `siteId`. Nếu client truyền `tenantId` hoặc `siteId` khác null, server từ chối ngay lập tức với `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`).

#### Request Body Structure

```json
{
  "paymentHoldDurationMinutes": 5,
  "minReservationDurationMinutes": 15,
  "maxReservationDurationHours": 24,
  "maxAdvanceBookingDays": 7,
  "interBookingBufferMinutes": 0,
  "reservationProtectionWindowHours": 4,
  "allocationLeadTimeMinutes": 60,
  "facilityFailureRefundLeadMinutes": 15,
  "occupancyValidityTimespanHours": 4,
  "arrivalLateToleranceMinutes": 15,
  "rapidCancellationCount": 2,
  "rapidCancellationWindowMinutes": 15,
  "rapidCancellationRestrictionMinutes": 180,
  "dailyCancellationLimit": 5,
  "cancellationTimezone": "Asia/Ho_Chi_Minh",
  "overrideBounds": {
    "ARRIVAL_LATE_TOLERANCE": { "minimum": 15, "maximum": 30 },
    "RESERVATION_MIN_DURATION": { "minimum": 15, "maximum": 60 },
    "PAYMENT_HOLD_DURATION": { "minimum": 5, "maximum": 15 }
  }
}
```

#### Fields Validation & Bounds Specification Table (M2 Closed)

| Trường | Kiểu / Đơn vị | Bắt buộc | Ràng buộc Domain & DB |
|---|---|---|---|
| `minReservationDurationMinutes` | `integer (minutes)` | Bắt buộc | $> 0$. |
| `maxReservationDurationHours` | `integer (hours)` | Bắt buộc | $> 0$ và thỏa mãn: `maxReservationDurationHours * 60 >= minReservationDurationMinutes`. |
| `paymentHoldDurationMinutes` | `integer (minutes)` | Bắt buộc | $> 0$. |
| `maxAdvanceBookingDays` | `integer (days)` | Bắt buộc | $\ge 0$. |
| `interBookingBufferMinutes` | `integer (minutes)` | Bắt buộc | $\ge 0$. Giá trị 0 là hợp lệ (cho phép liền kề). |
| `reservationProtectionWindowHours` | `integer (hours)` | Bắt buộc | $\ge 0$. |
| `allocationLeadTimeMinutes` | `integer (minutes)` | Bắt buộc | $\ge 0$. |
| `facilityFailureRefundLeadMinutes` | `integer (minutes)` | Bắt buộc | $\ge 0$. |
| `occupancyValidityTimespanHours` | `integer (hours)` | Bắt buộc | $\ge 0$. |
| `arrivalLateToleranceMinutes`| `integer (minutes)` | Bắt buộc | Phải trong khoảng $15 \le value \le 30$. |
| `rapidCancellationCount` | `integer (count)` | Bắt buộc | $> 0$. |
| `rapidCancellationWindowMinutes` | `integer (minutes)` | Bắt buộc | $> 0$. |
| `rapidCancellationRestrictionMinutes` | `integer (minutes)` | Bắt buộc | $> 0$. |
| `dailyCancellationLimit` | `integer (count)` | Bắt buộc | $> 0$. |
| `cancellationTimezone` | `string` | Bắt buộc | Bắt buộc là `"Asia/Ho_Chi_Minh"`. |
| `overrideBounds` | `object` | Bắt buộc | Map chứa các bounds cho đúng 10 keys cho phép. Cấm chứa key lạ hoặc key chống lạm dụng $\rightarrow$ `400 POLICY_BOUNDS_VIOLATED`. Đơn vị các bounds đều là **minutes**. |

**Quy tắc Ràng buộc Khả dụng Số nguyên (Integer Representability Validation — M2 Closed):**
Do các cột trong database được lưu trữ với đơn vị gốc (`hours`, `days`, `minutes`) theo schema PostgreSQL (`03b-reservation-operations-service-db.sql`), khoảng biên `[minimum, maximum]` (tính bằng phút) được Admin cấu hình trong `overrideBounds` phải thỏa mãn điều kiện toán học: **tồn tại ít nhất một giá trị số nguyên khả dụng trong đơn vị lưu trữ của cột tương ứng**, đồng thời không áp đặt các trần nghiệp vụ nhân tạo trái với Catalogue v1.1:
1. **Trường lưu số giờ nguyên (`hours`):** `RESERVATION_MAX_DURATION`, `RESERVATION_PROTECTION_WINDOW`, `OCCUPANCY_VALIDITY_TIMESPAN`:
   - Khoảng $[minimum, maximum]$ phút hợp lệ khi và chỉ khi tồn tại số giờ nguyên $h$ sao cho $minimum \le h \times 60 \le maximum$, tương đương điều kiện: $\lfloor maximum / 60 \rfloor \ge \lceil minimum / 60 \rceil$.
   - **Không bắt buộc** $minimum$ hay $maximum$ phải chia hết cho 60.
     - *Ví dụ hợp lệ:* $[61, 121]$ phút $\rightarrow \lceil 61/60 \rceil = 2 \le \lfloor 121/60 \rfloor = 2$ (chứa đúng một giá trị giờ nguyên khả dụng là $h = 2$ giờ hay 120 phút) $\rightarrow$ **HỢP LỆ**.
     - *Ví dụ không hợp lệ:* $[61, 119]$ phút $\rightarrow \lceil 61/60 \rceil = 2 > \lfloor 119/60 \rfloor = 1$ (không chứa bất kỳ số giờ nguyên nào) $\rightarrow$ **BỊ TỪ CHỐI** (`400 POLICY_BOUNDS_VIOLATED`).
   - Ràng buộc dương/không âm theo schema:
     - Với `RESERVATION_MAX_DURATION`: $h$ bắt buộc $> 0$, do đó điều kiện là: $\lfloor maximum / 60 \rfloor \ge \max(1, \lceil minimum / 60 \rceil)$.
     - Với `RESERVATION_PROTECTION_WINDOW` và `OCCUPANCY_VALIDITY_TIMESPAN`: $h \ge 0$, do đó điều kiện là: $\lfloor maximum / 60 \rfloor \ge \max(0, \lceil minimum / 60 \rceil)$.
2. **Trường lưu số ngày nguyên (`days`):** `MAX_ADVANCE_BOOKING`:
   - **Tầng lưu trữ & Khả dụng số nguyên:** Cột DB `max_advance_booking_days INT NULL CHECK (max_advance_booking_days >= 0)` cho phép giá trị không âm (kể cả 0 ngày tại tầng lưu trữ). Khoảng $[minimum, maximum]$ phút hợp lệ khi và chỉ khi tồn tại số ngày nguyên $d \ge 0$ sao cho $minimum \le d \times 1440 \le maximum$, tương đương điều kiện: $\lfloor maximum / 1440 \rfloor \ge \max(0, \lceil minimum / 1440 \rceil)$.
     - *Ví dụ hợp lệ:* $[0, 0]$ phút (chứa $d = 0$ ngày), $[0, 1440]$ phút (chứa $d = 0$ và $d = 1$ ngày), $[1000, 2000]$ phút (chứa $d = 1$ ngày = 1440 phút).
     - *Ví dụ không hợp lệ:* $[100, 1000]$ phút (không chứa số ngày nguyên nào vì $0 < 100$ và $1440 > 1000$) $\rightarrow$ **BỊ TỪ CHỐI** (`400 POLICY_BOUNDS_VIOLATED`).
   - **Ý nghĩa Nghiệp vụ & Hợp đồng Domain cho FE & QA (Domain Advance Horizon Contract):**
     Theo quy tắc của Domain (`ReservationInterval.cs`): Điều kiện đặt chỗ hợp lệ là `Start > now && Start - now <= advanceHorizon`.
     - Nếu cấu hình `maxAdvanceBookingDays = 0` ngày ($advanceHorizon = 0$ phút): Điều kiện tương đương `Start > now && Start - now <= 0` (bất khả thi về mặt toán học). Do đó, cấu hình 0 ngày có nghĩa là **tính năng đặt trước hoàn toàn bị đóng** (cửa sổ đặt trước bằng 0, không có lượt đặt chỗ tương lai nào có thể được tạo, kể cả cùng ngày).
     - Để cho phép tài xế đặt chỗ cho các khung giờ còn lại trong ngày hiện tại, Platform hoặc Bãi xe **bắt buộc phải cấu hình `maxAdvanceBookingDays >= 1` ngày** (hoặc biên phút $> 0$). FE cần hiển thị thông điệp cảnh báo rõ ràng khi Admin chọn 0 ngày ("Đóng tính năng đặt trước").
3. **Các trường lưu phút (`minutes`):**
   - `ARRIVAL_LATE_TOLERANCE`: Bắt buộc $15 \le minimum \le maximum \le 30$ phút theo đúng SRS và CHECK constraint.
   - `RESERVATION_MIN_DURATION` và `PAYMENT_HOLD_DURATION`: Bắt buộc số nguyên $> 0$, tức là $1 \le minimum \le maximum$.
   - `INTER_BOOKING_BUFFER`, `ALLOCATION_LEAD_TIME`, `FACILITY_FAILURE_REFUND_LEAD_TIME`: Bắt buộc số nguyên $\ge 0$, tức là $0 \le minimum \le maximum$.
4. **Không áp đặt trần nghiệp vụ tùy tiện:** Hệ thống tôn trọng quyền cấu hình biên của Admin theo Catalogue v1.1; không tự ý áp đặt trần 168 giờ hay 30 ngày. Admin có toàn quyền quyết định mức trần kỹ thuật cho bãi xe của hệ thống miễn là $minimum \le maximum$ và thỏa mãn tính khả dụng số nguyên trên.

#### Response Sample (201 Created)

```json
{
  "success": true,
  "data": {
    "id": "27300000-0000-4000-8000-000000000099",
    "version": 2,
    "scope": "PLATFORM",
    "status": "DRAFT",
    "isActive": false,
    "createdAt": "2026-11-12T10:15:00.0000000Z",
    "updatedAt": "2026-11-12T10:15:00.0000000Z"
  }
}
```

---

### 5. `PUT /api/v1/admin/policies/operations/draft/{id}`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `PUT /api/v1/admin/policies/operations/draft/{id}` |
| **Availability** | Proposed (SPARK-368 / SPARK-340 / M4 Closed) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | **Platform Scope Only** (`tenant_id IS NULL AND site_id IS NULL`) |
| **Content Type** | Request & Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>`, `If-Match: "<updatedAt_timestamp>"` (Tùy chọn nếu truyền trong body) |

#### Khóa Lạc quan & Thứ tự Kiểm tra Quyền Scope (C1 & M4 Closed)
1. **Quy tắc Concurrency Token:**
   - Client bắt buộc gửi token phiên bản qua header `If-Match` hoặc trường `expectedUpdatedAt` trong JSON body (định dạng ISO 8601 với 7 chữ số mili/micro giây UTC: `yyyy-MM-ddTHH:mm:ss.fffffffZ`).
   - Nếu client gửi cả hai: header `If-Match` và trường `expectedUpdatedAt` trong body **phải có giá trị giống hệt nhau**. Nếu sai lệch $\rightarrow$ `400 INVALID_REQUEST` ("If-Match header and expectedUpdatedAt body property must match.").
   - Nếu client không gửi cả hai $\rightarrow$ `400 VALIDATION_FAILED` ("Concurrency token expectedUpdatedAt or If-Match header is required for draft updates.").
2. **Thứ tự Đánh giá Nghiệp vụ & Quyền Ưu tiên (Execution Order):**
   - **Bước A (Truy vấn Kiểm tra Target):**
     Application Handler đọc metadata của bản ghi:
     `SELECT id, tenant_id, site_id, is_active, updated_at FROM operational_policies WHERE id = @Id;`
     - Nếu không tìm thấy $\rightarrow$ trả về `404 Not Found` (`POLICY_NOT_FOUND`).
     - **ƯU TIÊN 1 (Scope Authorization):** Nếu `target.TenantId != null || target.SiteId != null` $\rightarrow$ **ngay lập tức trả về `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`)**, không thực hiện bất kỳ thao tác nào tiếp theo (bất kể timestamp gửi lên có đúng hay sai).
     - **ƯU TIÊN 2 (Active State):** Nếu `target.IsActive == true` $\rightarrow$ trả về `409 Conflict` (`CANNOT_MODIFY_ACTIVE_POLICY`, message: `"This policy revision has already been activated and cannot be modified."`).
     - **ƯU TIÊN 3 (Concurrency Match):** Nếu `target.UpdatedAt != expectedUpdatedAt` $\rightarrow$ trả về `409 Conflict` (`RESOURCE_VERSION_CONFLICT`, message: `"The draft was modified by another session. Please reload and review the current draft before saving."`).
   - **Bước B (Thực thi Ghi Nguyên tử & Xử lý Nhánh 0 Hàng - Zero-Rows Race Handling — M4 Closed):**
     1. **Câu lệnh UPDATE có trả về Token thực:**
        ```sql
        UPDATE operational_policies
        SET payment_hold_duration_minutes = @PaymentHoldDurationMinutes,
            min_reservation_duration_minutes = @MinReservationDurationMinutes,
            max_reservation_duration_hours = @MaxReservationDurationHours,
            max_advance_booking_days = @MaxAdvanceBookingDays,
            inter_booking_buffer_minutes = @InterBookingBufferMinutes,
            reservation_protection_window_hours = @ReservationProtectionWindowHours,
            allocation_lead_time_minutes = @AllocationLeadTimeMinutes,
            facility_failure_refund_lead_minutes = @FacilityFailureRefundLeadMinutes,
            occupancy_validity_timespan_hours = @OccupancyValidityTimespanHours,
            arrival_late_tolerance_minutes = @ArrivalLateToleranceMinutes,
            rapid_cancellation_count = @RapidCancellationCount,
            rapid_cancellation_window_minutes = @RapidCancellationWindowMinutes,
            rapid_cancellation_restriction_minutes = @RapidCancellationRestrictionMinutes,
            daily_cancellation_limit = @DailyCancellationLimit,
            override_bounds = @OverrideBoundsJson::jsonb,
            updated_at = GREATEST(CURRENT_TIMESTAMP, @ExpectedUpdatedAt + interval '1 microsecond')
        WHERE id = @Id
          AND tenant_id IS NULL
          AND site_id IS NULL
          AND is_active = false
          AND updated_at = @ExpectedUpdatedAt
        RETURNING updated_at;
        ```
     2. **Xử lý Kết quả Thực thi:**
        - **Trường hợp Thành công (`rowsAffected == 1`):** Trả về `200 OK` kèm concurrency token mới chính là giá trị `updated_at` thực tế được PostgreSQL sinh và trả về từ mệnh đề `RETURNING`. Biểu thức `GREATEST(CURRENT_TIMESTAMP, @ExpectedUpdatedAt + interval '1 microsecond')` bảo đảm toán học rằng token mới luôn **nghiêm ngặt lớn hơn token cũ** ($T_{new} > @ExpectedUpdatedAt$), kể cả khi đồng hồ hệ thống chạy trong cùng transaction hoặc độ phân giải thời gian bị lặp, bảo đảm 100% token bị thay đổi sau mỗi lần ghi thành công.
        - **Trường hợp Xung đột Đồng thời (`rowsAffected == 0`):**
          Khi hai request PUT cùng vượt qua Bước A (pre-check), hoặc có một lệnh `activate` chen ngang giữa Bước A và Bước B, câu lệnh UPDATE sẽ trả về 0 hàng (`rowsAffected == 0`).
          Tầng Application **tuyệt đối không trả lỗi chung chung**, mà ngay lập tức thực hiện truy vấn tái kiểm tra trạng thái hiện hành (`Re-check Current State`):
          ```sql
          SELECT id, tenant_id, site_id, is_active, updated_at FROM operational_policies WHERE id = @Id;
          ```
          Phân loại kết quả trả về cho Client theo đúng trạng thái thực tế:
          - Nếu bản ghi không còn tồn tại $\rightarrow$ Trả về `404 Not Found` (`POLICY_NOT_FOUND`).
          - Nếu bản ghi là Site Scope (`tenant_id != null || site_id != null`) $\rightarrow$ Trả về `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`).
          - Nếu bản ghi đã chuyển sang `is_active == true` (do lệnh activate đã thực thi và commit trước trong khoảng giữa Bước A và B) $\rightarrow$ Trả về `409 Conflict` (`CANNOT_MODIFY_ACTIVE_POLICY`, message: `"This policy revision has been activated concurrently by another administrator and cannot be modified."`).
          - Nếu bản ghi có `updated_at != expectedUpdatedAt` (do một request PUT song song đã ghi thành công trước) $\rightarrow$ Trả về `409 Conflict` (`RESOURCE_VERSION_CONFLICT`, message: `"The draft was modified concurrently by another session. Please reload and review the current draft before saving."`).

3. **Kịch bản Kiểm thử Concurrency & Scope cho QA (M4 Closed):**
   - **Kịch bản QA 1 (Hai PUT song song đồng bộ sau Bước A):**
     Admin 1 và Admin 2 cùng tải Draft Revision 2 tại thời điểm $T_0$ (`updatedAt = 2026-11-12T10:15:00.0000000Z`).
     Cả hai request cùng gửi PUT lên server và cùng vượt qua Bước A (pre-check).
     - Request của Admin 1 thực thi Bước B trước: `rowsAffected = 1`, database ghi nhận `updated_at = T_1` ($T_1 > T_0$), server trả về `200 OK` kèm token $T_1$.
     - Request của Admin 2 thực thi Bước B sau: do `updated_at` trong DB đã đổi sang $T_1$, UPDATE trả về `rowsAffected = 0`. Handler re-check DB phát hiện `updated_at (T_1) != expectedUpdatedAt (T_0)` $\rightarrow$ trả về `409 Conflict` (`RESOURCE_VERSION_CONFLICT`), bảo vệ dữ liệu khỏi lost update.
   - **Kịch bản QA 2 (PUT song song với Activate đồng bộ sau Bước A):**
     Admin 1 gửi PUT đã vượt qua Bước A. Trong tích tắc trước khi Admin 1 thực thi Bước B, Admin 2 kích hoạt draft thành công (`POST /revisions/{id}/activate` commit với `is_active = true`).
     Bước B của Admin 1 trả về `rowsAffected = 0`. Handler re-check phát hiện `is_active == true` $\rightarrow$ trả về mã lỗi riêng biệt `409 Conflict` (`CANNOT_MODIFY_ACTIVE_POLICY`, thay vì RESOURCE_VERSION_CONFLICT) để thông báo rõ cho người dùng bản ghi đã chuyển sang active.
   - **Kịch bản QA 3 (Ưu tiên Quyền Scope trước Concurrency):**
     Admin cố ý gửi `PUT` tới một Site draft có ID hợp lệ và `expectedUpdatedAt` khớp chính xác với DB.
     Hệ thống kiểm tra `target.TenantId != null` trước tiên và từ chối với `403 FORBIDDEN` (`FORBIDDEN_SCOPE_WRITE`), không thực thi UPDATE và không thay đổi `updated_at`. Lặp lại với `expectedUpdatedAt` cũ vẫn trả về `403 FORBIDDEN_SCOPE_WRITE`, chứng minh lỗi quyền luôn được ưu tiên cao nhất.
   - **Kịch bản QA 4 (Bảo đảm Token Nghiêm Ngặt Tăng & Chặn Tái Sử Dụng Token Cũ):**
     Admin gửi PUT sửa draft với token $T_0$. Server ghi thành công và trả về $T_1$ với $T_1 > T_0$ (thông qua `GREATEST`). Admin gửi tiếp request PUT thứ hai nhưng vẫn dùng token cũ $T_0$. Câu lệnh UPDATE trả về `rowsAffected = 0`. Re-check DB phát hiện `updated_at` hiện hành ($T_1$) $\ne T_0 \rightarrow$ trả về `409 Conflict` (`RESOURCE_VERSION_CONFLICT`), chứng minh token cũ bị hủy hiệu lực ngay lập tức sau đúng 1 lần ghi.

#### Request Body Structure
```json
{
  "expectedUpdatedAt": "2026-11-12T10:15:00.0000000Z",
  "paymentHoldDurationMinutes": 10,
  "minReservationDurationMinutes": 15,
  "maxReservationDurationHours": 24,
  "maxAdvanceBookingDays": 7,
  "interBookingBufferMinutes": 0,
  "reservationProtectionWindowHours": 4,
  "allocationLeadTimeMinutes": 60,
  "facilityFailureRefundLeadMinutes": 15,
  "occupancyValidityTimespanHours": 4,
  "arrivalLateToleranceMinutes": 15,
  "rapidCancellationCount": 2,
  "rapidCancellationWindowMinutes": 15,
  "rapidCancellationRestrictionMinutes": 180,
  "dailyCancellationLimit": 5,
  "cancellationTimezone": "Asia/Ho_Chi_Minh",
  "overrideBounds": {
    "ARRIVAL_LATE_TOLERANCE": { "minimum": 15, "maximum": 30 },
    "RESERVATION_MIN_DURATION": { "minimum": 15, "maximum": 60 },
    "PAYMENT_HOLD_DURATION": { "minimum": 5, "maximum": 15 }
  }
}
```

#### Response Sample (200 OK)

```json
{
  "success": true,
  "message": "Platform policy draft updated successfully.",
  "data": {
    "id": "27300000-0000-4000-8000-000000000099",
    "version": 2,
    "scope": "PLATFORM",
    "status": "DRAFT",
    "isActive": false,
    "updatedAt": "2026-11-12T10:30:00.0000000Z"
  }
}
```

---

### 6. `POST /api/v1/admin/policies/operations/revisions/{id}/activate`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `POST /api/v1/admin/policies/operations/revisions/{id}/activate` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | **Platform Scope Only** |
| **Content Type** | Request & Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Request Body Structure

```json
{
  "effectiveFrom": "2026-12-01T00:00:00Z"
}
```
*Ghi chú:* Nếu `effectiveFrom` là `null` hoặc bỏ qua, hệ thống sẽ sử dụng mốc thời gian máy chủ `DateTimeOffset.UtcNow` để kích hoạt ngay lập tức.

#### Thuật toán Kích hoạt Nguyên tử & Ràng buộc Interval (M3 Closed)
1. **Xác định Mốc Thời gian Máy chủ Duy nhất:**
   - Lấy `serverNow = DateTimeOffset.UtcNow`.
   - Nếu `effectiveFrom == null`: đặt `targetEffectiveFrom = serverNow`.
   - Nếu `effectiveFrom != null`:
     - Nếu `effectiveFrom < serverNow` $\rightarrow$ từ chối với `400 VALIDATION_FAILED` ("Effective start timestamp cannot be in the past.").
     - Ngược lại: đặt `targetEffectiveFrom = effectiveFrom`.
2. **Thực thi Giao dịch Nguyên tử (Atomic Transaction với Row Locks):**
   - Mở Transaction và thực hiện khóa cả bản ghi mục tiêu lẫn các bản ghi active:
     ```sql
     -- Khóa bản ghi mục tiêu
     SELECT id, tenant_id, site_id, is_active, updated_at
     FROM operational_policies
     WHERE id = @Id FOR UPDATE;
     ```
     - Nếu không tìm thấy $\rightarrow$ `404 POLICY_NOT_FOUND`.
     - Nếu `tenant_id != null || site_id != null` $\rightarrow$ `403 FORBIDDEN_SCOPE_WRITE` (Admin không được kích hoạt Site draft).
     - Nếu `is_active == true` $\rightarrow$ **Rollback và trả về `422 CANNOT_ACTIVATE_NON_DRAFT`** (Xử lý dứt điểm trường hợp 2 lệnh kích hoạt cùng gửi tới 1 draft; lệnh thứ hai sẽ bị từ chối ngay lập tức mà không làm gap lịch).
   - Khóa các bản ghi active của Platform:
     ```sql
     SELECT * FROM operational_policies
     WHERE tenant_id IS NULL AND site_id IS NULL AND is_active = true
     FOR UPDATE;
     ```
   - **Kiểm tra Trùng Mốc Bắt Đầu:** Nếu tồn tại bất kỳ active revision nào có `effective_from == targetEffectiveFrom` $\rightarrow$ Rollback và trả về `409 Conflict` (`OVERLAPPING_POLICY_INTERVAL`, message: `"An active or scheduled revision already starts at this exact timestamp."`).
   - **Tìm Tiền nhiệm $P$ Phủ $T$:**
     Tìm duy nhất revision $P$ thỏa mãn: `P.effective_from < targetEffectiveFrom AND (P.effective_until IS NULL OR P.effective_until > targetEffectiveFrom)`.
   - **Tìm Kế nhiệm $S$:**
     Tìm revision $S$ thỏa mãn: `effective_from > targetEffectiveFrom` với mốc bắt đầu nhỏ nhất.
   - **Phân Bổ Interval:**
     - Nếu $P$ tồn tại: Cập nhật $P.effective\_until = targetEffectiveFrom$, $P.updated\_at = serverNow$.
     - Gán $Target.effective\_from = targetEffectiveFrom$.
     - Nếu $S$ tồn tại: Gán $Target.effective\_until = S.effective\_from$.
     - Nếu không có $S$: Gán $Target.effective\_until = null$.
     - Gán $Target.is\_active = true$, $Target.updated\_at = serverNow$.
   - **Trường hợp Thiếu Tiền nhiệm $P$ (Missing Predecessor — kể cả khi Platform chưa có active row nào):**
     - Nếu hệ thống mới khởi tạo chưa có active row nào hoặc $targetEffectiveFrom$ được kích hoạt trước phiên bản active sớm nhất: Không có bản ghi nào bị đóng khoảng (`supersededRevision: null` trong response).
     - Bản ghi mục tiêu được kích hoạt với `effective_from = targetEffectiveFrom` và `effective_until = (S != null ? S.effective_from : null)`.
     - Gán `Target.is_active = true`, `Target.updated_at = serverNow`.
   - **Commit Transaction & Bảo đảm QA (M3 Closed):**
     - Toàn bộ lịch được cập nhật nguyên tử, không có khoảng hở và không bị trùng lấn.
     - **Bảo đảm QA về một lần chuyển trạng thái duy nhất:** Khi gửi đồng thời hai request kích hoạt tới cùng một draft ID tại thời điểm $T_1$ và $T_2$, do target draft được khóa `FOR UPDATE` trong cùng một transaction, request thứ nhất thành công chuyển `is_active = true`. Request thứ hai đọc thấy `is_active == true` $\rightarrow$ Rollback toàn bộ và trả về `422 Unprocessable` (`CANNOT_ACTIVATE_NON_DRAFT`), hoàn toàn không làm thay đổi lịch hay để lại bất kỳ khoảng hở (gap) nào.

#### Response Sample (200 OK — Kích hoạt Thành công)

```json
{
  "success": true,
  "data": {
    "id": "27300000-0000-4000-8000-000000000099",
    "version": 2,
    "scope": "PLATFORM",
    "status": "SCHEDULED",
    "isActive": true,
    "effectiveFrom": "2026-12-01T00:00:00Z",
    "effectiveUntil": null,
    "supersededRevision": {
      "id": "27300000-0000-4000-8000-000000000001",
      "version": 1,
      "effectiveUntil": "2026-12-01T00:00:00Z"
    }
  }
}
```

#### Response Sample (409 Conflict — Trùng mốc Bắt đầu)

```json
{
  "success": false,
  "code": "OVERLAPPING_POLICY_INTERVAL",
  "message": "Activation rejected: An active or scheduled policy revision (Version 2) already starts at the exact timestamp 2026-12-01T00:00:00Z.",
  "traceId": "c83fe191b4ad47f8b9e4a3b1287e0231",
  "errors": [
    {
      "property": "effectiveFrom",
      "message": "Exact start time collision with scheduled Revision 2."
    }
  ]
}
```

---

### Group 2: Quản trị Tích hợp Cổng Thanh toán (`SmartParking.PaymentService.API`)

---

### 7. `GET /api/v1/admin/integrations/payment`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `GET /api/v1/admin/integrations/payment` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Platform payment integration settings |
| **Content Type** | Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Bảng Quy Tắc Trạng Thái Health theo Mode & Credentials (M6 Closed)

| Mode | Tình trạng Credentials | Kết quả Probe gần nhất | `status` | `isConfigured` | `lastTestedAt` | Hành vi Mạng Outbound |
|---|---|---|---|---|---|---|
| `FIXTURE` | Bất kỳ (Bị bỏ qua) | Không áp dụng (N/A) | `HEALTHY` | `true` | `null` | **0 outbound calls**. Giả lập local URL `http://localhost:5173/payment/success?...` |
| `SANDBOX` | Thiếu $\ge 1$ required key | Chưa probe | `MISCONFIGURED` | `false` | `null` | Chuyển an toàn về local emulation |
| `SANDBOX` | Đủ 3 keys | Vừa nạp/rotate, chưa probe | `UNTESTED` | `true` | `null` | Gọi sandbox API tới PayOS endpoint |
| `SANDBOX` | Đủ 3 keys | Probe thành công | `HEALTHY` | `true` | `timestamp` | Gọi sandbox API tới PayOS endpoint |
| `SANDBOX` | Đủ 3 keys | Probe thất bại / Timeout | `DEGRADED` | `true` | `timestamp` | Gọi sandbox API; ghi log sanitize |
| `LIVE` | Thiếu $\ge 1$ required key | Chưa probe | `MISCONFIGURED` | `false` | `null` | Từ chối tạo payment request |
| `LIVE` | Đủ 3 keys | Vừa nạp/rotate, chưa probe | `UNTESTED` | `true` | `null` | Gọi live API tới PayOS endpoint |
| `LIVE` | Đủ 3 keys | Probe thành công | `HEALTHY` | `true` | `timestamp` | Gọi live API tới PayOS endpoint |
| `LIVE` | Đủ 3 keys | Probe thất bại / Timeout | `DEGRADED` | `true` | `timestamp` | Gọi live API; ghi log sanitize |

#### Request Sample
```http
GET /api/v1/admin/integrations/payment HTTP/1.1
Host: payment.smartpark.internal
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
Accept: application/json
```

#### Response Sample (200 OK — Sandbox Mode với credentials đã được probe thành công)

```json
{
  "success": true,
  "data": {
    "provider": "PAYOS",
    "mode": "SANDBOX",
    "baseUrl": "https://api-merchant.payos.vn",
    "isConfigured": true,
    "clientIdMasked": "****9876",
    "apiKeyConfigured": true,
    "checksumKeyConfigured": true,
    "status": "HEALTHY",
    "lastTestedAt": "2026-11-12T09:30:00Z",
    "capabilities": {
      "supportsVietQr": true,
      "supportsWebhook": true,
      "supportsDirectRefund": false
    }
  }
}
```

#### Bảng Mô tả Thuộc tính Response

| Trường | Kiểu / Format | Bắt buộc / Nullable | Ý nghĩa & Bảo mật | Ví dụ |
|---|---|---|---|---|
| `provider` | `string` | Bắt buộc | Tên nhà cung cấp cổng thanh toán. | `"PAYOS"` |
| `mode` | `string (enum)` | Bắt buộc | Chế độ: `"LIVE"`, `"SANDBOX"`, `"FIXTURE"`. | `"SANDBOX"` |
| `baseUrl` | `string (uri)` | Bắt buộc | Endpoint API của đối tác đã được kiểm tra allowlist. | `"https://api-merchant.payos.vn"` |
| `isConfigured` | `boolean` | Bắt buộc | `true` nếu toàn bộ credentials bắt buộc của mode hiện tại đã có. | `true` |
| `clientIdMasked` | `string` | Nullable | Client ID đã được che giấu (`****` + 4 ký tự cuối). Null nếu chưa cấu hình. | `"****9876"` |
| `apiKeyConfigured` | `boolean` | Bắt buộc | Báo hiệu đã lưu API Key cho mode này hay chưa. Không bao giờ echo raw key. | `true` |
| `checksumKeyConfigured` | `boolean` | Bắt buộc | Báo hiệu đã lưu Checksum Key cho mode này hay chưa. Không bao giờ echo raw key. | `true` |
| `status` | `string (enum)` | Bắt buộc | Trạng thái: `"HEALTHY"`, `"MISCONFIGURED"`, `"UNTESTED"`, `"DEGRADED"`. | `"HEALTHY"` |
| `lastTestedAt` | `string (date-time)`| Nullable | Mốc thời gian lần probe hoặc giao dịch thành công gần nhất. Null nếu chưa test. | `"2026-11-12T09:30:00Z"` |
| `capabilities` | `object` | Bắt buộc | Các tính năng thanh toán được hỗ trợ của adapter hiện tại. | `{ "supportsVietQr": true, ... }` |

---

### 8. `PUT /api/v1/admin/integrations/payment`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `PUT /api/v1/admin/integrations/payment` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Platform payment integration update |
| **Content Type** | Request & Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Quy tắc An toàn, BaseUrl Allowlist & Health Transition (C2 & M6 Closed)
- `mode`: Bắt buộc thuộc tập `["LIVE", "SANDBOX", "FIXTURE"]`.
- `baseUrl`: Bắt buộc thuộc `AllowedBaseUrls[mode]`. Nếu sai $\rightarrow$ `400 Bad Request` (`INVALID_PAYMENT_BASE_URL`).
- **Cô lập Credential:**
  - Cập nhật thông tin vào cấu hình của chính mode đó (`LiveCredentials` hoặc `SandboxCredentials`).
  - Khi mode đổi sang `FIXTURE`: Không cần credentials, hệ thống tắt toàn bộ kết nối outbound.
  - Khi mode đổi sang `SANDBOX`: Nạp `SandboxCredentials` độc lập, không dùng chung với `LiveCredentials`.
- **Quy tắc Trạng thái Response Thống nhất với GET (Health State Consistency — M6 Closed):**
  Lệnh PUT cập nhật cấu hình và trả về response với trạng thái `status` tuân thủ **chính xác 100% theo Bảng Quy Tắc Trạng Thái Health (§5.7)** của phiên bản vừa lưu, loại bỏ hoàn toàn hiện tượng lệch trạng thái giữa PUT và GET:
  1. Khi chuyển sang `mode == "FIXTURE"`: Trả về ngay `status: "HEALTHY"`, `isConfigured: true`, `lastTestedAt: null` (vì chế độ FIXTURE hoàn toàn không cần keys và không gọi mạng). Client refresh trang gọi GET vẫn giữ nguyên `HEALTHY`.
  2. Khi chuyển sang `mode == "SANDBOX"` hoặc `"LIVE"` mà thiếu $\ge 1$ required key: Trả về ngay `status: "MISCONFIGURED"`, `isConfigured: false`, `lastTestedAt: null`. Client refresh trang gọi GET vẫn giữ nguyên `MISCONFIGURED`.
  3. Khi chuyển sang `mode == "SANDBOX"` hoặc `"LIVE"` với đủ 3 keys: Do lệnh PUT không thực hiện outbound probe ngay lúc lưu, trả về `status: "UNTESTED"`, `isConfigured: true`, `lastTestedAt: null` (cho đến khi có lệnh probe chủ động hoặc giao dịch thực tế đầu tiên).
- **Bảo vệ Checkout Đang Xử Lý & Vòng Đời Signing Key theo Keyring (Keyring Retention & In-Flight Protection — M6 Closed):**
  - **Quản lý Phiên bản Signing Key theo Keyring:** Thay vì chỉ giữ duy nhất một previous key trong 1 giờ (có nguy cơ mất key nếu xoay vòng 2 lần liên tiếp $K_0 \rightarrow K_1 \rightarrow K_2$), `PaymentService` quản lý một danh mục khóa ký tích cực (**Active Keyring**). Mỗi khóa ký khi tạo được gán `signing_key_version` (ví dụ $K_0, K_1, K_2$) kèm thời điểm kích hoạt (`active_from`) và thời điểm ngừng nhận checkout mới (`retired_at`).
  - **Ghi nhận Phiên bản Khóa Ký theo Đơn hàng:** Khi tạo đơn thanh toán checkout (`POST /v2/payment-requests`), bảng `payment_orders` ghi nhận trường `signing_key_version = current_version` và `mode` tại thời điểm phát hành.
  - **Thu hồi key:** Áp dụng đồng thời điều kiện thời gian và vòng đời checkout ở §3.4 cho từng cặp `(mode, signing_key_version)`, kể cả khi rotate nhiều lần liên tiếp.
  - **Ràng Buộc Xác Thực Webhook Chặt Chẽ theo Order Binding (Order-Key Binding Invariant — M6 Closed):**
    Khi webhook IPN từ PayOS gửi về kèm `orderCode`:
    1. **Tra cứu Đơn hàng:** Adapter truy vấn `payment_orders` theo `orderCode`.
       - Order không tồn tại: trả `404 ORDER_NOT_FOUND`, kết thúc xử lý và giữ nguyên toàn bộ trạng thái tài chính.
    2. **Xác thực Độc Quyền theo Key Version của Đơn hàng:**
       - Bản ghi `payment_order` lưu trữ bất biến `signing_key_version` và `mode` tại thời điểm tạo đơn.
       - Adapter lấy đúng `ChecksumKey` tương ứng với phiên bản và mode đó từ Keyring để tính và so sánh chữ ký HMAC-SHA256.
       - **Nếu chữ ký KHÔNG KHỚP với key của chính đơn hàng đó:**
         - Ngay lập tức từ chối callback với mã `400 Bad Request` (`INVALID_WEBHOOK_SIGNATURE`).
         - **TUYỆT ĐỐI KHÔNG thử tiếp các key khác trong Keyring cho đơn hàng này** (kể cả khi chữ ký đó có thể hợp lệ dưới current key $K_1$ hay $K_2$). Việc thử key khác làm suy yếu binding mật mã của đơn hàng và bị nghiêm cấm theo D11-B.
         - Giữ nguyên trạng thái hiện tại của order và số khoản thu đã ghi nhận.
    3. **Kiểm tra khoản thu:** Sau khi xác thực chữ ký, Payment kiểm tra trạng thái giao dịch của provider, số tiền/đơn vị tiền và liên kết giữa chứng cứ thu tiền với order, reservation và điều khoản đã chấp nhận. Kết quả khác `SUCCESS` không chuyển order sang `PAID`. Số tiền sai trả mã `AMOUNT_MISMATCH`; chứng cứ mâu thuẫn được lưu để đối soát, không phát sự kiện thu tiền thành công cho booking. [Handler Payment](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ProcessWebhook/ProcessPayOsWebhookCommandHandler.cs)
    4. **Ghi nhận và phát sự kiện:** Với mỗi khoản thu thành công đã xác minh, Payment ghi collection và outbox trong cùng transaction. Giao lại cùng định danh collection với cùng dữ liệu trả kết quả đã ghi; dữ liệu mâu thuẫn phải đối soát. Hai khoản thu thực sự khác nhau trên cùng order được ghi nhận riêng, kể cả khi order đã `PAID`, `EXPIRED` hoặc `CANCELLED`. Operations xử lý sự kiện theo collection ID, kiểm lại binding và quyết định xác nhận hoặc đối soát theo §6.1. Khoản thu thêm không tạo quyền đỗ thứ hai. [Hợp đồng đích D11-B](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md)

**Ca nghiệm thu webhook cho SPARK-370** (đặc tả đích). Mỗi ca quan sát cả trạng thái order, số collection và sự kiện logic trong outbox.

| Đầu vào | Kết quả cần quan sát |
| --- | --- |
| Order K0, đã rotate K0 → K1 → K2; callback ký K0, SUCCESS, đúng số tiền và binding, collection mới | Một collection và một sự kiện logic. Operations tiếp tục kiểm tra claim theo §6.1. |
| Cùng order nhưng chỉ xác minh được bằng K1 | `400 INVALID_WEBHOOK_SIGNATURE`; trạng thái order, collection và outbox giữ nguyên. |
| Đúng K0, provider báo giao dịch thất bại | Không chuyển order sang PAID; không tạo collection thành công hay sự kiện xác nhận. |
| Đúng K0, SUCCESS nhưng sai số tiền | `AMOUNT_MISMATCH`; lưu chứng cứ, không xác nhận booking. |
| Gửi lại cùng collection đã xử lý, cùng nội dung | Kết quả idempotent; số collection, hiệu ứng nghiệp vụ và sự kiện logic không tăng. |
| Khoản thu đã xác minh có định danh khác trên order đã thanh toán | Ghi nhận khoản thu bổ sung và outbox riêng để đối soát; booking giữ một entitlement. |

#### Request Body Structure

```json
{
  "mode": "SANDBOX",
  "baseUrl": "https://api-merchant.payos.vn",
  "clientId": "321a-456b-789c-9876",
  "apiKey": "e9876543-21ba-4321-fedc-ba9876543210",
  "checksumKey": "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0"
}
```

#### Response Sample A (200 OK — Cập nhật mode SANDBOX với đủ keys trả về UNTESTED)

```json
{
  "success": true,
  "message": "Payment integration settings updated successfully. Credentials saved for mode SANDBOX.",
  "data": {
    "provider": "PAYOS",
    "mode": "SANDBOX",
    "baseUrl": "https://api-merchant.payos.vn",
    "isConfigured": true,
    "clientIdMasked": "****9876",
    "apiKeyConfigured": true,
    "checksumKeyConfigured": true,
    "status": "UNTESTED",
    "lastTestedAt": null
  }
}
```

#### Response Sample B (200 OK — Chuyển sang mode FIXTURE trả về HEALTHY, 0 outbound calls)

```json
{
  "success": true,
  "message": "Payment integration settings updated successfully. Switched to FIXTURE mode (zero outbound HTTP).",
  "data": {
    "provider": "PAYOS",
    "mode": "FIXTURE",
    "baseUrl": "http://localhost:5173",
    "isConfigured": true,
    "clientIdMasked": null,
    "apiKeyConfigured": false,
    "checksumKeyConfigured": false,
    "status": "HEALTHY",
    "lastTestedAt": null
  }
}
```

---

### Group 3: Giám sát Trạng thái Delivery Adapters (`SmartParking.NotificationAuditService.API`)

---

### 9. `GET /api/v1/admin/integrations/notifications`

| Mục | Hợp đồng kỹ thuật |
|---|---|
| **Method / URL** | `GET /api/v1/admin/integrations/notifications` |
| **Availability** | Proposed (SPARK-368 / SPARK-340) |
| **Authentication** | Bearer JWT |
| **Freshness / Origin** | `TOKEN` |
| **Permission** | Role: `ADMIN` |
| **Resource Scope** | Notification channels delivery status |
| **Content Type** | Response: `application/json; charset=utf-8` |
| **Headers** | `Authorization: Bearer <TOKEN>` |

#### Phân định Phạm vi Nghiệp vụ (AC-5 Alignment)
- Endpoint này cung cấp trạng thái quan sát (**Read-Only Monitoring**) của 4 kênh delivery adapter hiện hữu trong `NotificationAuditService`. Việc chuyển đổi adapter vật lý sang các nhà cung cấp Cloud (như Twilio, AWS SES, Firebase Cloud Messaging) thuộc phạm vi các story mở rộng sau MVP.

#### Bảng Mô tả Thuộc tính Response & Trạng thái Kênh

| Trường | Kiểu / Format | Bắt buộc / Nullable | Ý nghĩa & Enum |
|---|---|---|---|
| `channels[]` | `array` | Bắt buộc | Danh sách các kênh gửi thông báo của hệ thống. |
| `channels[].channel` | `string (enum)` | Bắt buộc | Mã kênh: `"IN_APP"`, `"EMAIL"`, `"SMS"`, `"PUSH"`. |
| `channels[].provider` | `string` | Bắt buộc | Tên lớp Adapter thực thi (ví dụ `"LOCAL_EMAIL_CAPTURE_FIXTURE"`). |
| `channels[].isReady` | `boolean` | Bắt buộc | `true` nếu adapter đã khởi tạo và sẵn sàng nhận bản tin dispatch. |
| `channels[].status` | `string (enum)` | Bắt buộc | Trạng thái kênh: `"ACTIVE"`, `"DEGRADED"`, `"UNAVAILABLE"`, `"DISABLED"`. |
| `channels[].description` | `string` | Bắt buộc | Mô tả chức năng và mục đích của kênh delivery. |
| `channels[].lastTestedAt` | `string (date-time)` | Nullable | Thời điểm kiểm tra hoặc dispatch thông báo thành công gần nhất. |

#### Response Sample (200 OK — Minh họa Đa trạng thái ACTIVE, DEGRADED, UNAVAILABLE)

```json
{
  "success": true,
  "data": {
    "channels": [
      {
        "channel": "IN_APP",
        "provider": "IN_APP_FIXTURE",
        "isReady": true,
        "status": "ACTIVE",
        "description": "Lưu thông báo trực tiếp vào bảng notifications để hiển thị trên web portal.",
        "lastTestedAt": "2026-11-12T10:00:00Z"
      },
      {
        "channel": "EMAIL",
        "provider": "LOCAL_EMAIL_CAPTURE_FIXTURE",
        "isReady": true,
        "status": "ACTIVE",
        "description": "Bắt và ghi nhận email hóa đơn / biên lai vào bộ nhớ cục bộ phục vụ kiểm thử và đối soát.",
        "lastTestedAt": "2026-11-12T09:45:00Z"
      },
      {
        "channel": "SMS",
        "provider": "LOCAL_SMS_FIXTURE",
        "isReady": false,
        "status": "DEGRADED",
        "description": "Giả lập gửi SMS mã OTP. Đang gặp lỗi quá tải bộ nhớ đệm giả lập.",
        "lastTestedAt": "2026-11-12T08:30:00Z"
      },
      {
        "channel": "PUSH",
        "provider": "LOCAL_PUSH_FIXTURE",
        "isReady": false,
        "status": "UNAVAILABLE",
        "description": "Kênh WebPush / App notification chưa khởi tạo socket daemon.",
        "lastTestedAt": null
      }
    ]
  }
}
```

---

## 6. Concurrency, Invariants & Snapshot Guarantees

### 6.1 Tính Bất biến của Snapshot & 3 Kịch bản Cắt qua Mốc $T_{policy}$ (M5 Closed)
Hệ thống SmartPark bảo đảm tính bất biến tuyệt đối của các giao dịch và đặt chỗ đã cam kết, tuân thủ nghiêm ngặt **Quyết định Kiến trúc ngày 11/11/2026 (D11-A, D11-B)** và hợp đồng luồng `Operations` ([Booking API addendum 11/11](reservation-first-slice-api-design.md)):
1. **Giai đoạn Review Điều khoản (`POST /api/reservation-terms`):**
   - Khi tài xế xem trước điều khoản, `OperationService` trích xuất chính sách active tại mốc thời gian backend đánh giá (`decided_at`) và lưu vào bảng `reservation_terms.snapshot_json` (JSONB) kèm hạn hiệu lực xem trước `valid_until`.
   - Tại bước này, **chưa có payment hold hay slot hold nào được commit**, không tạo thực thể `reservations`.
2. **Giai đoạn Tiếp nhận Yêu cầu & Cam kết Giữ chỗ (Admission & Hold Commitment — D11-A):**
   - Tài xế bấm đặt chỗ qua `POST /api/reservation-requests` kèm `termsId`, `termsVersion`, idempotency key.
   - Endpoint thực hiện xác thực người dùng và tiếp nhận bền vững với mã **`202 Accepted`** cùng header `Location: /api/reservation-requests/{requestId}` (trả về request receipt trạng thái `PENDING_AUTHORIZATION`). Mã `202` chỉ chứng nhận yêu cầu đã được tiếp nhận hợp lệ vào hệ thống, **chưa phải là cam kết giữ chỗ (hold)**.
   - Background worker `BookingProgressHandler` xử lý yêu cầu:
     - Đối chiếu phiên bản policy trong terms đã lưu với phiên bản policy đang active tại thời điểm xử lý.
     - Nếu phát hiện phiên bản policy của terms ($V_1$) đã cũ so với phiên bản active ($V_2$, do Admin vừa kích hoạt revision mới giữa chừng): worker từ chối yêu cầu và cập nhật trạng thái receipt thành **`REJECTED`** với lý do **`TERMS_CHANGED_OR_EXPIRED`** (hoàn toàn không có phản hồi HTTP 409 đồng bộ ở bước này vì API đã trả về 202). Tài xế quan sát kết quả qua `GET /api/reservation-requests/{requestId}` và được yêu cầu tải lại điều khoản mới ($V_2$) qua `POST /api/reservation-terms`.
      - Nếu terms hợp lệ ($V_{terms} == V_{active}$): Worker tạo bản ghi `ReservationRow` với trạng thái **`PENDING_PAYMENT`**, đóng băng `accepted_operational_policy_snapshot`, cam kết giữ chỗ tạm thời với mốc tạo `hold_created_at = serverNow` và hạn thanh toán chuẩn xác:
        $$H = \min(hold\_created\_at + PaymentHoldDurationMinutes, T_{start})$$
        (Tuân thủ nghiêm ngặt [Policy Catalogue v1.1](../../01-requirements/business-rules/policy-catalogue.md) và `ReservationDeadlines.cs` dòng 27: hạn thanh toán hold bắt buộc bị chặn trần bởi giờ bắt đầu đỗ $T_{start}$). Trạng thái receipt chuyển sang **`HOLD_CREATED`** (theo đề xuất D11-A và addendum Booking để phân định rõ ràng giữa tiến trình request admission với vòng đời đặt chỗ).
3. **Giai đoạn Xác nhận Sau Thu Tiền & Quy trình Phục hồi (Event-Driven Confirmation Workflow & Recovery — D11-B & D11-D):**
   - Đặt chỗ chuyển sang trạng thái chờ thanh toán trong thời gian giữ chỗ $H$. `PaymentService` xử lý đơn hàng và nhận thanh toán từ PayOS.
   - Payment thực hiện kiểm tra và ghi collection/outbox theo §5.8. Operations loại sự kiện lặp và xác minh binding giữa collection, reservation, order, số tiền và terms trước khi xét quyền xác nhận.
   - **Checkpoint D11-C/D/E:** Operations dùng một mốc DB `evaluatedAt` tại bước kiểm tra cuối dưới khóa. Với hold còn hạn, tiếp tục phối hợp Facility tại bước confirmation và kiểm capacity, plate, site cùng claim hiện hành. Chỉ dùng fast path thuần aggregate sau khi gate D11-C có bằng chứng implementation và QA được duyệt. [Quyết định xác nhận 11/11](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md)

| Điều kiện tại checkpoint của Operations | Kết quả |
| --- | --- |
| `evaluatedAt < H` và claim còn được bảo vệ, các kiểm tra Facility/Operations thành công | Commit `CONFIRMED` với snapshot đã chấp nhận. Client thấy quyền đỗ sau commit. |
| `evaluatedAt < T_start` và hold đã hết hạn, claim bị vô hiệu hoặc không chứng minh được quyền giữ chỗ (kể cả trước H) | Theo D11-D, tái thẩm định và giành lại claim tương thích dưới khóa với Facility và facts hiện hành. Chỉ xác nhận nếu thành công. Nếu thất bại, giữ khoản thu đã xác minh và chuyển đối soát/hoàn tiền. |
| `evaluatedAt >= T_start` và booking chưa được xác nhận | Không cấp quyền đỗ mới; tiếp nhận xử lý đối soát cho collection. Việc tiếp nhận đối soát chưa chứng minh đã hoàn tiền. |

Tại `now >= H`, Payment dừng tạo checkout mới và tiếp tục ghi nhận tiền thực tế đã xác minh. Một checkpoint thành công trước `T_start` có thể commit sau mốc này; nếu transaction rollback, lần thử mới lấy lại giờ DB. Lượt đặt miễn phí theo terms hợp lệ đi qua lệnh xác nhận không thu tiền và cùng các guard của Operations.

**3 Kịch bản Kiểm thử Chuẩn cho QA (M5 Closed):**
- **Kịch bản A (Hold đã commit trước $T_{policy}$, Tiền về và xác nhận sau $T_{policy}$):**
  - Đặt chỗ được tiếp nhận và commit hold `PENDING_PAYMENT` tại $t_{hold} < T_{policy}$ dưới chính sách Version 1.
  - Platform kích hoạt Version 2 tại $T_{policy}$ ($t_{hold} < T_{policy}$).
  - Khi collection và binding đã xác minh, nếu `evaluatedAt < H`, claim còn được bảo vệ và các kiểm tra Facility/Operations thành công, Operations commit `CONFIRMED` với snapshot $V_1$.
  - **Ca đối chứng trước H:** Vô hiệu claim sau hold commit, trước khi xử lý collection. QA phải quan sát tái thẩm định; nếu giành lại claim tương thích trước `T_start` thì xác nhận với $V_1$. Nếu claim cạnh tranh thắng thì booking không được xác nhận, collection đi vào đối soát. Cả hai kết quả giữ nguyên snapshot đã nhận.
- **Kịch bản B (Review & Xác nhận đều sau $T_{policy}$):**
  - Tài xế review điều khoản và đặt chỗ đều diễn ra tại $t > T_{policy}$.
  - **Kết quả nghiệm thu:** Cả hai bước đều nạp, áp dụng và đóng băng **Version 2**.
- **Kịch bản C (Review trước $T_{policy}$ under $V_1$, nhưng Submit Đặt chỗ sau $T_{policy}$ under $V_2$):**
  - Tài xế xem điều khoản lúc $t_{review} < T_{policy}$ (nhận snapshot Version 1).
  - Đến $t_{submit} > T_{policy}$, tài xế gửi `POST /api/reservation-requests`.
  - **Kết quả nghiệm thu:**
    1. Response HTTP của `POST /api/reservation-requests`: Trả về **`202 Accepted`** kèm header `Location: /api/reservation-requests/{requestId}` (durable admission receipt).
    2. Response HTTP `200 OK` của `GET /api/reservation-requests/{requestId}` bọc DTO trong `data`. [ReservationRequestsController](../../../src/Services/OperationService/SmartParking.OperationService.API/Controllers/ReservationRequestsController.cs)
       ```json
       {
         "success": true,
         "data": {
           "requestId": "98765432-1111-4000-8000-000000000001",
           "state": "REJECTED",
           "reservationId": null,
           "code": "TERMS_CHANGED_OR_EXPIRED"
         }
       }
       ```
    3. Sau khi receipt đã terminal, Newman kiểm tra `data.state == "REJECTED"`, `data.code == "TERMS_CHANGED_OR_EXPIRED"` và `data.reservationId == null`.
       - Trong Integration Test: Truy vấn DB `SELECT COUNT(*) FROM reservations WHERE request_id = @requestId;` trả về 0 (chứng minh không có bất kỳ hàng đặt chỗ nào được tạo ra). Tài xế tải lại điều khoản mới ($V_2$) qua `POST /api/reservation-terms`.

### 6.2 PostgreSQL GiST Exclusion Constraint & Khớp Sentinel UUID (M3 Closed)
Bảng `operational_policies` thiết lập ràng buộc loại trừ GiST khớp chính xác 100% với schema `03b-reservation-operations-service-db.sql`:

```sql
CONSTRAINT operational_policy_effective_scope EXCLUDE USING gist (
    (COALESCE(tenant_id, '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
    (COALESCE(site_id, '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
    tstzrange(effective_from, effective_until, '[)') WITH &&
) WHERE (is_active);
```

**Nguyên lý Khoảng Nửa Mở $[effective\_from, effective\_until)$:**
- Toán tử `[)` có nghĩa là bao gồm mốc bắt đầu nhưng không bao gồm mốc kết thúc.
- Do đó, nếu phiên bản $V_1$ có khoảng $[2026-11-01T00:00:00Z, 2026-12-01T00:00:00Z)$ và phiên bản $V_2$ có khoảng $[2026-12-01T00:00:00Z, \infty)$, toán tử giao nhau `&&` trong PostgreSQL trả về `FALSE` (không trùng lấn).
- Trong Application Handler:
  1. Khi kích hoạt $V_2$ tại $T = 2026-12-01T00:00:00Z$, handler cập nhật $V_1.effective\_until = T$.
  2. Gán $V_2.effective\_from = T$, $V_2.is\_active = true$.
  3. Cả hai thao tác nằm trong cùng một DbTransaction với row lock `FOR UPDATE`.
  4. Nếu có một request song song cố tình ghi đè một khoảng khác giao với $[T, \infty)$, GiST constraint sẽ phát hiện vi phạm và trả về `PostgresException (SqlState: 23P01)`.
  5. `ReservationExceptionHandler` bắt lỗi `23P01` trên constraint `operational_policy_effective_scope` và chuyển đổi thành `409 Conflict` (`OVERLAPPING_POLICY_INTERVAL`).

### 6.3 Đăng ký & Ánh xạ Exception Phục vụ SPARK-368 (`ReservationExceptionHandler.cs` Delta — M7 Closed)
**Hiện trạng Codebase:** `ReservationExceptionHandler.cs` hiện đang xử lý chung mọi lỗi Postgres `ExclusionViolation` / `UniqueViolation` thành `(409, "RESOURCE_CONFLICT", ...)` và `ValidationException` thành `(400, "INVALID_REQUEST", ...)`.
**Mapping Đích Proposed (Target Scoped Delta):**
Trong khuôn khổ triển khai SPARK-368, `ReservationExceptionHandler.cs` sẽ được mở rộng có phạm vi (scoped mapping):
1. **Ánh xạ Constraint GiST Loại trừ Scoped:**
   Khi bắt được `PostgresException` có `SqlState == PostgresErrorCodes.ExclusionViolation` (`23P01`) (hoặc bọc bên trong `DbUpdateException`):
   - Nếu `ConstraintName == "operational_policy_effective_scope"`: chuyển đổi thành mã chuyên biệt `(409, "OVERLAPPING_POLICY_INTERVAL", "Activation rejected: The specified effective interval overlaps with another active or scheduled policy revision.")`.
   - Các constraint khác của hệ thống: giữ nguyên mã hiện tại `(409, "RESOURCE_CONFLICT", ...)`.
2. **Ánh xạ Validation Scoped theo Route:**
   Khi request URL bắt đầu bằng `/api/v1/admin/policies/operations`:
   - Nếu `ValidationException` vi phạm các quy tắc về bounds: trả về `(400, "POLICY_BOUNDS_VIOLATED", "One or more policy override bounds are invalid.")`.
   - Các vi phạm validation khác của request body: trả về `(400, "VALIDATION_FAILED", "Request validation failed.")` kèm mảng chi tiết `errors`.
3. **Đăng ký các Domain Exceptions mới vào bảng tra cứu `Known`:**
   - `ForbiddenScopeException` $\rightarrow$ `(403, "FORBIDDEN_SCOPE_WRITE", error.Message)`
   - `CannotModifyActivePolicyException` $\rightarrow$ `(409, "CANNOT_MODIFY_ACTIVE_POLICY", error.Message)`
   - `CannotActivateNonDraftException` $\rightarrow$ `(422, "CANNOT_ACTIVATE_NON_DRAFT", error.Message)`
   - `ResourceVersionConflictException` $\rightarrow$ `(409, "RESOURCE_VERSION_CONFLICT", error.Message)`

---

## 7. Sequence Diagrams

### Sequence 1: Tạo Bản thảo, Khóa Lạc quan Sửa Bản thảo và Kích hoạt Nguyên tử

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quản trị viên (Admin)
    participant FE as Frontend (Admin Console)
    participant API as OperationService.API
    participant APP as Application Handler (MediatR)
    participant REPO as IReservationPolicyRepository
    participant DB as PostgreSQL (operational_policies)

    Note over Admin,DB: BƯỚC 1: TẠO BẢN THẢO (POST /draft)
    Admin->>FE: 1. Nhập tham số Platform & Bounds mới
    FE->>API: 2. POST /api/v1/admin/policies/operations/draft (Payload)
    API->>APP: 3. CreatePolicyDraftCommand
    APP->>APP: 4. FluentValidation (chặn siteId, tính khả dụng số nguyên của bounds, late tolerance 15..30)
    APP->>REPO: 5. CreateDraftAsync(draftRow)
    REPO->>DB: 6. INSERT INTO operational_policies (is_active = false)
    DB-->>REPO: 7. Lưu thành công Revision v2
    REPO-->>APP: 8. Trả về Id & Version 2
    APP-->>API: 9. Result(DraftResponse)
    API-->>FE: 10. 201 Created { id, version: 2, status: "DRAFT" }

    Note over Admin,DB: BƯỚC 2: CẬP NHẬT BẢN THẢO VỚI KHÓA LẠC QUAN (PUT /draft/{id})
    Admin->>FE: 11. Chỉnh sửa tham số hold time từ 5 -> 10 phút
    FE->>API: 12. PUT /api/v1/admin/policies/operations/draft/{id} (Payload, expectedUpdatedAt)
    API->>APP: 13. UpdatePolicyDraftCommand(id, expectedUpdatedAt, Payload)
    APP->>REPO: 14. GetMetadataByIdAsync(id)
    REPO->>DB: 15. SELECT id, tenant_id, site_id, is_active, updated_at FROM operational_policies WHERE id = @id
    DB-->>REPO: 16. Trả về metadata
    alt Target là Site Draft (tenant_id != null || site_id != null)
        APP-->>API: 17a. ForbiddenScopeException
        API-->>FE: 18a. 403 Forbidden { code: "FORBIDDEN_SCOPE_WRITE" }
    else Target đã kích hoạt (is_active == true)
        APP-->>API: 17b. CannotModifyActiveException
        API-->>FE: 18b. 409 Conflict { code: "CANNOT_MODIFY_ACTIVE_POLICY" }
    else Timestamp không khớp (updated_at != expectedUpdatedAt)
        APP-->>API: 17c. ConcurrencyException
        API-->>FE: 18c. 409 Conflict { code: "RESOURCE_VERSION_CONFLICT" }
    else Thỏa mãn pre-check
        APP->>REPO: 17d. UpdateDraftOptimisticAsync(...)
        REPO->>DB: 18d. UPDATE operational_policies SET updated_at = GREATEST(CURRENT_TIMESTAMP, @expectedUpdatedAt + interval '1 microsecond') WHERE id = @id AND tenant_id IS NULL AND site_id IS NULL AND is_active = false AND updated_at = @expectedUpdatedAt RETURNING updated_at
        alt 1 row updated (Thành công)
            DB-->>REPO: 19d. 1 row updated, committed_updated_at
            REPO-->>APP: 20d. Success(committed_updated_at)
            APP-->>API: 21d. Result(UpdatedDraftDto)
            API-->>FE: 22d. 200 OK { updatedAt: committed_updated_at }
        else 0 rows updated (Mất race sau pre-check)
            DB-->>REPO: 19e. 0 rows updated
            REPO->>DB: 20e. SELECT is_active, updated_at FROM operational_policies WHERE id = @id
            DB-->>REPO: 21e. Current state
            alt Bản ghi vừa bị kích hoạt (is_active == true)
                REPO-->>APP: 22e. ActiveConflict
                APP-->>API: 23e. CannotModifyActiveException
                API-->>FE: 24e. 409 Conflict { code: "CANNOT_MODIFY_ACTIVE_POLICY" }
            else Bản ghi vừa bị sửa (updated_at != expectedUpdatedAt)
                REPO-->>APP: 22f. VersionConflict
                APP-->>API: 23f. ConcurrencyException
                API-->>FE: 24f. 409 Conflict { code: "RESOURCE_VERSION_CONFLICT" }
            end
        end
    end

    Note over Admin,DB: BƯỚC 3: KÍCH HOẠT NGUYÊN TỬ (POST /revisions/{id}/activate)
    Admin->>FE: 23. Nhấn "Kích hoạt" (EffectiveFrom = 2026-12-01T00:00:00Z)
    FE->>API: 24. POST /api/v1/admin/policies/operations/revisions/{id}/activate
    API->>APP: 25. SchedulePolicyRevisionCommand(id, effectiveFrom)
    APP->>REPO: 26. BeginTransactionAsync()
    APP->>REPO: 27. LockTargetAndActiveRevisionsAsync(id, effectiveFrom)
    REPO->>DB: 28. SELECT * FROM operational_policies WHERE id = @id FOR UPDATE;<br/>SELECT * FROM operational_policies WHERE tenant_id IS NULL AND is_active = true FOR UPDATE;
    DB-->>REPO: 29. Trả về Target Row & Active Revisions
    alt Target là Site Draft (tenant_id != null || site_id != null)
        APP->>REPO: 30a. RollbackAsync()
        APP-->>API: 31a. ForbiddenScopeException
        API-->>FE: 32a. 403 Forbidden { code: "FORBIDDEN_SCOPE_WRITE" }
    else Target đã kích hoạt (is_active == true)
        APP->>REPO: 30b. RollbackAsync()
        APP-->>API: 31b. CannotActivateException
        API-->>FE: 32b. 422 Unprocessable { code: "CANNOT_ACTIVATE_NON_DRAFT" }
    else Trùng mốc bắt đầu (effective_from == targetEffectiveFrom)
        APP->>REPO: 30c. RollbackAsync()
        APP-->>API: 31c. OverlapConflictException
        API-->>FE: 32c. 409 Conflict { code: "OVERLAPPING_POLICY_INTERVAL" }
    else Phân bổ Interval hợp lệ
        APP->>REPO: 30d. ClosePredecessorIntervalAsync(P.Id, targetEffectiveFrom)
        REPO->>DB: 31d. UPDATE P SET effective_until = '2026-12-01T00:00:00Z'
        APP->>REPO: 32d. ActivateRevisionAsync(Target.Id, targetEffectiveFrom, S.effective_from)
        REPO->>DB: 33d. UPDATE Target SET is_active = true, effective_from = '2026-12-01T00:00:00Z'
        APP->>REPO: 34d. CommitTransactionAsync()
        DB-->>REPO: 35d. Commit OK
        APP-->>API: 36d. Result(ActivationSuccess)
        API-->>FE: 37d. 200 OK { id, version: 2, status: "SCHEDULED" }
    end
```

---

### Sequence 2: Giải quyết Chính sách Hiệu lực (Effective Policy Resolution)

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quản trị viên (Admin Console)
    participant API as OperationService.API
    participant APP as GetEffectivePoliciesQueryHandler
    participant REPO as IReservationPolicyRepository
    participant DB as PostgreSQL (operational_policies)
    participant Resolver as ReservationPolicyResolver (Domain)

    Admin->>API: 1. GET /api/v1/admin/policies/operations/effective?tenantId=...&siteId=...
    API->>APP: 2. Dispatch GetEffectivePoliciesQuery(tenantId, siteId, at)
    APP->>REPO: 3. GetActiveRevisionAsync(scope: PLATFORM, at)
    REPO->>DB: 4. SELECT * FROM operational_policies WHERE tenant_id IS NULL AND is_active AND at <@ range
    DB-->>REPO: 5. Trả về PlatformRow v1
    REPO-->>APP: 6. PlatformPolicyRevision v1
    APP->>REPO: 7. GetActiveRevisionAsync(scope: SITE, tenantId, siteId, at)
    REPO->>DB: 8. SELECT * FROM operational_policies WHERE tenant_id = ... AND site_id = ... AND is_active
    alt Bãi xe chưa từng có revision nào
        DB-->>REPO: 9a. Trả về null
        REPO-->>APP: 10a. null
        APP-->>API: 11a. NotFoundException("Site policy revision not found")
        API-->>Admin: 12a. 404 Not Found { code: "POLICY_NOT_FOUND" }
    else Bãi xe đã có revision
        DB-->>REPO: 9b. Trả về SiteRow v1
        REPO-->>APP: 10b. SitePolicyRevision v1
        APP->>Resolver: 11b. ReservationPolicyResolver.Resolve(platformRev, siteRev)
        Resolver->>Resolver: 12b. Kiểm tra bounds, RequiredKeys, tính kế thừa
        Resolver-->>APP: 13b. Trả về EffectiveReservationPolicy
        APP->>APP: 14b. Đóng gói DTO (gắn nhãn inherited, source, isZeroNotDisabled)
        APP-->>API: 15b. Result(EffectivePolicyDto)
        API-->>Admin: 16b. 200 OK { scope: "SITE", values: { ... }, arrivalLateTolerance: { ... } }
    end
```

---

### Sequence 3: Xem và Cập nhật Tích hợp Cổng Thanh toán PayOS với Secret Masking & BaseUrl Whitelist

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Quản trị viên (Admin)
    participant FE as Frontend (System Config)
    participant API as PaymentService.API
    participant APP as PaymentIntegrationHandler
    participant SEC as Security & Options Service
    participant Audit as ILogger / Audit Trail

    Note over Admin,Audit: XEM TRẠNG THÁI CẤU HÌNH (GET)
    Admin->>FE: 1. Mở trang Quản trị Tích hợp
    FE->>API: 2. GET /api/v1/admin/integrations/payment
    API->>APP: 3. GetPaymentIntegrationSettingsQuery
    APP->>SEC: 4. Lấy cấu hình PayOS hiện tại theo mode
    SEC->>SEC: 5. Masking: ClientId -> "****9876", giấu raw ApiKey & ChecksumKey
    SEC-->>APP: 6. Trả về PaymentIntegrationDto an toàn
    APP-->>API: 7. Result(Dto)
    API-->>FE: 8. 200 OK (clientIdMasked, apiKeyConfigured: true, status: "HEALTHY")

    Note over Admin,Audit: CẬP NHẬT CẤU HÌNH AN TOÀN (PUT)
    Admin->>FE: 9. Nhập BaseUrl mới và ApiKey mới cho Sandbox
    FE->>API: 10. PUT /api/v1/admin/integrations/payment (mode, baseUrl, apiKey)
    API->>APP: 11. UpdatePaymentIntegrationCommand
    APP->>APP: 12. Kiểm tra BaseUrl trong AllowedBaseUrls[mode]
    alt BaseUrl không thuộc Whitelist
        APP-->>API: 13a. ValidationException("Invalid BaseUrl")
        API-->>FE: 14a. 400 Bad Request { code: "INVALID_PAYMENT_BASE_URL" }
    else BaseUrl hợp lệ
        APP->>SEC: 13b. Lưu bền vững cấu hình mode SANDBOX (cô lập khỏi LIVE)
        APP->>Audit: 14b. Log kiểm toán (ghi nhận scope, configVersion, actorId - KHÔNG GHI RAW KEY)
        SEC-->>APP: 15b. Cập nhật thành công (status xác định theo bảng §5.7: FIXTURE -> HEALTHY; SANDBOX/LIVE đủ key -> UNTESTED; thiếu key -> MISCONFIGURED)
        APP-->>API: 16b. Result(UpdatedMaskedDto)
        API-->>FE: 17b. 200 OK { success: true, message: "...", data: { status: "UNTESTED / HEALTHY" } }
    end
```

---

## 8. Open Questions & Deferred Items

| Mã | Hạng mục | Tình trạng quyết định | Tác động | Ghi chú & Căn cứ kỹ thuật |
|---|---|---|---|---|
| **Q-01** | `WRONG_PARKING_FEE` | DEFERRED | Bảng `operational_policies` | Theo quyết định ngày 2026-11-09, công thức và mức phí phạt đỗ sai vị trí được hoãn sang giai đoạn sau; chưa thêm vào catalogue v1.0/v1.3 này. |
| **Q-02** | Camera AI Recognition Confidence Threshold | DEFERRED (POST-v1.0) | `IoTService` | Theo [FR-POL-09](../../01-requirements/srs/modules/04-reservations-allocation.md#fr-pol-09) và [UC-POL-02](../../01-requirements/use-cases/uc-pol-02.md), việc cấu hình độ nhạy nhận diện biển số thuộc giai đoạn hậu v1.0; không tạo endpoint cấu hình phần cứng giả lập trong MVP này. |
| **Q-03** | Tự động hóa hoàn tiền trực tiếp qua PayOS API | DEFERRED | `PaymentService` | PayOS hiện tại sử dụng VietQR một chiều cho thanh toán; quy trình hoàn tiền (Refund) vẫn thực hiện qua đối soát nghiệp vụ và chuyển khoản thủ công có lưu bằng chứng kế toán. |

---

## 9. Review checks

| Phạm vi | Căn cứ nghiệm thu cho SPARK-368, SPARK-369 và SPARK-370 |
| --- | --- |
| Phân quyền và catalogue | 9 endpoint dùng ADMIN/TOKEN. Site write bị từ chối cả khi timestamp đúng. Đối chiếu 14 key, bounds v1, nguồn giá trị và zero/disabled với seed và resolver. |
| Draft và lịch | Token tăng sau mỗi UPDATE thành công; FE dùng token thực từ RETURNING. Kiểm stale edit, activate xen ngang và hai lệnh activate cùng draft. Kiểm khoảng kề nhau và lỗi GiST theo scope. |
| Tích hợp PayOS | Kiểm health sau PUT/GET, redaction trên đường thành công/lỗi, tách key theo mode, các ca callback và outbox ở §5.8. |
| Snapshot và xác nhận | Kiểm receipt stale terms, deadline H, claim còn hiệu lực và claim bị vô hiệu trước H theo §6.1. Đối chiếu D11-C trước khi thay đổi Facility coordination. |
| Bằng chứng | Bản v1.6 đã được Lead Architect phê duyệt chính thức (APPROVED — IMPLEMENTATION READY). Sẵn sàng bàn giao cho Phase 2 (SPARK-368), Phase 3 (SPARK-369) và Phase 4 (SPARK-370). |
