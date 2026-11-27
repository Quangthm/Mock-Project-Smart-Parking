# Monthly & Yearly Parking Products (Configuration & Handoff Contract) — API Design

| Metadata | Value |
| --- | --- |
| Status | DRAFT v1.1 (Aligned with `origin/develop` S2S RS256 Service JWT & Coding Skill 2026-11-16) |
| Contract version / date | v1.1 / 2026-11-17 |
| Owning service / author | `OperationService` (plan & entitlement) + `PaymentService` (financial terms & refunds) / Nguyễn Tường Văn |
| Jira design / implementation / verification | [SPARK-331](https://mangamanagement.atlassian.net/browse/SPARK-331) (`SPARK-341` BE, `SPARK-342` FE, `SPARK-343` Test) · Consumer: [SPARK-332](https://mangamanagement.atlassian.net/browse/SPARK-332) (`SPARK-344`, `SPARK-345`, `SPARK-346`) |
| Requirements baseline | SRS v1.1 [07-monthly-passes.md](../../01-requirements/srs/modules/07-monthly-passes.md); Business Rules [M-01..M-09](../../01-requirements/business-rules/07-monthly-passes.md); [ownership-and-events.md](../../02-architecture/service-boundaries/ownership-and-events.md); [DB28](../../02-architecture/database/alignment-backlog.md) |
| Implementation | NOT IMPLEMENTED (Contract handoff prior to `SPARK-341` / `SPARK-344` coding) |

## Overview

Tài liệu này đặc tả **API Contract & Database Schema** cho cụm tính năng cấu hình sản phẩm đỗ xe theo tháng/năm (`SPARK-331` / `US-OW06`) và các **read / internal `SERVICE` contracts** bàn giao trực tiếp cho luồng Driver mua & kích hoạt gói (`SPARK-332` / `US-D13`). Thiết kế tuân thủ ranh giới v1.1 (`V11-10`): `OperationService` sở hữu định nghĩa gói, phạm vi, trạng thái bật/tắt bán và quyền lợi (`WHEN_SPACE_AVAILABLE`, không giữ chỗ); `PaymentService` sở hữu giá trọn kỳ, điều khoản hoàn tiền, đối soát tiền và báo cáo thu/hoàn tiền của Owner.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | `FR-MON-01..06`, `FR-MON-12..13`; `M-01..M-07`, `M-09`; `UC-MON-01`, `UC-MON-02`, `UC-MON-05`; `US-OW06`, `US-D13` |
| Permission / service ownership | `OperationService` (`LOT_POLICY_MANAGE` / plan lifecycle) · `PaymentService` (financial terms, orders, refunds) · `UserService` (`REQUEST` authorization in `332`) |
| Approved decisions & S2S standard | `V11-10`; [authorization-operation-classification.md](../../01-requirements/srs/authorization-operation-classification.md); [SmartPark-Coding-Skill-v1.0.md](../../07-skills/SmartPark-Coding-Skill-v1.0.md) (§4 Service-to-service authentication); [reservation-facility-dependency-api-design.md](reservation-facility-dependency-api-design.md); `V11-DES-05` / `Q05` |

> [!IMPORTANT]
> **Nguyên tắc bắt buộc cho cả `SPARK-331` và `SPARK-332`:**
> 1. **Tách biệt Enablement và Financial Terms:** Bật gói bên `OperationService` và cấu hình giá/hoàn tiền bên `PaymentService` là 2 thao tác độc lập. Gói chỉ có thể bán/hiển thị cho Driver khi **cả hai** đều hợp lệ và đã publish.
> 2. **Available-Only (`WHEN_SPACE_AVAILABLE`):** Không giữ chỗ đỗ hay chiếm công suất bãi khi cấu hình, đặt mua hay kích hoạt gói.
> 3. **Không có giá mặc định:** `1,200,000 VND` chỉ là dữ liệu test mẫu (fixture), tuyệt đối không dùng làm default production.
> 4. **Yearly (`Q05` / `V11-DES-05`) đang bị BLOCKED thương mại hóa:** Mọi API và DB schema hỗ trợ cấu trúc `productType = "YEARLY"`, nhưng thao tác bật bán thật (`enablement`) và báo giá/checkout `YEARLY` phải bị chặn (`422 YEARLY_POLICY_BLOCKED_Q05`) cho đến khi `Q05` được phê duyệt. Không copy mặc định của gói tháng sang gói năm; không dùng `1,200,000 VND` làm default.
> 5. **Chuẩn xác thực S2S mới nhất (`Authorization: Bearer <service JWT>` — Cấm `X-Service-Key`):**
>    - Theo [SmartPark-Coding-Skill-v1.0.md §4](../../07-skills/SmartPark-Coding-Skill-v1.0.md) và triển khai `OperationsServiceAuthentication` trên `origin/develop` (`PaymentService`), **mọi internal HTTP capability mới tuyệt đối không dùng `X-Service-Key`**.
>    - Caller (`OperationService`) ký **short-lived Service JWT (`RS256`, TTL 60s)** bằng RSA private key riêng (`>= 2048 bit`). Service nhận (`PaymentService`) xác thực bằng public key tương ứng (`clockSkew <= 5s`), kiểm tra đồng thời: `alg = RS256`, `iss = SmartParking.OperationService`, `sub = SmartParking.OperationService`, `sender = SmartParking.OperationService`, `aud = SmartParking.PaymentService`, `exp`/`nbf`, và claim `permission` theo từng capability. Driver JWT và `X-Service-Key` đều bị từ chối (`401` / `403`).
> 6. **Error Envelope theo từng service:**
>    - `OperationService`: `{ "success": false, "code": "...", "message": "...", "traceId": "..." }`.
>    - `PaymentService`: `{ "success": false, "code": "...", "message": "..." }`.
> 7. **Quy chuẩn tổ chức code (`2026-11-16`):** Mỗi file `.cs` mới/chỉnh sửa chỉ khai báo **1 named type** (`one C# named type per source file`), triển khai theo từng slice dọc nhỏ.

---

## Cross-Service Interaction (331 ↔ 332 & payOS Flow)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    actor Driver
    participant Ops as OperationService (331 & 332)
    participant Pay as PaymentService (331 & 332)
    participant User as UserService (REQUEST Saga)
    participant PayOS as payOS Gateway

    rect rgb(240, 248, 255)
    note right of Owner: Pha SPARK-331 (Owner cấu hình & phát hành gói)
    Owner->>Ops: PUT /api/v1/lots/{siteId}/pass-plans/{productType}/draft
    Owner->>Ops: POST /api/v1/lots/{siteId}/pass-plans/{productType}/publish (Idempotency-Key)
    Owner->>Pay: PUT /api/v1/pricing/lots/{siteId}/pass-plans/{planId}/financial-terms
    Owner->>Ops: PATCH /api/v1/lots/{siteId}/pass-plans/{productType}/enablement
    Ops->>Pay: GET /internal/pass-financial-terms?siteId=...&planId=... (Bearer RS256 Service JWT)
    Pay-->>Ops: Active financialTermId + revision (hoặc 404/422 nếu thiếu)
    Ops-->>Owner: 200 OK (Monthly enabled; Yearly blocked nếu Q05 chưa duyệt)
    end

    rect rgb(245, 255, 245)
    note right of Driver: Pha SPARK-332 (Driver xem, khóa snapshot & thanh toán qua payOS có sẵn)
    Driver->>Ops: GET /api/v1/sites/{siteId}/pass-offers
    Ops->>Pay: GET /internal/pass-financial-terms?siteId=... (Bearer RS256 Service JWT)
    Ops-->>Driver: Offers (planId, planRevision, financialTermId, financialTermRevision, price, anchorRule)

    Driver->>Ops: POST /api/v1/pass-subscriptions (SPARK-332 endpoint, kèm planRevision + financialTermRevision)
    Ops->>Ops: B2 In-Process Port: IPassPlanSnapshotService.ValidateAndFreezeAsync()
    Ops->>Pay: POST /internal/pass-financial-terms/quote-snapshot (Bearer RS256 Service JWT)
    Pay-->>Ops: FinancialTermSnapshot (giá + refundTerms khóa theo revision)
    Ops->>User: Async Account-current REQUEST (WorkflowId, AuthorizationAttemptId)
    Ops->>Pay: Tạo PaymentOrder (MONTHLY_PASS_FEE, expiresAt = H) qua Service JWT
    Driver->>Pay: POST /api/v1/payments/orders/{orderId}/checkout (API có sẵn)
    Pay->>PayOS: Tạo link VietQR (PayOsAdapter)
    Pay-->>Driver: checkoutUrl + qrCode
    PayOS->>Pay: POST /api/v1/payments/callbacks/payos (Webhook IPN có sẵn)
    Pay-->>Ops: Outbox event PaymentSucceeded -> Commit entitlement nếu A < H (ngược lại reconcile/refund)
    end
```

---

## API Specification — Phần A: Owner Configuration (`SPARK-331`)

### A1. Tra cứu cấu hình gói của bãi (`OperationService`)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/lots/{siteId}/pass-plans` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` (chấp nhận token freshness + kiểm tra quyền Owner theo `siteId`) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` (Owner sở hữu `siteId`) |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

No body. Path parameter `siteId` (UUID v4).

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "effectiveOperationalParams": {
      "monthlyMaxPrepaidTerms": 3,
      "monthlyPaymentHoldMinutes": 15,
      "monthlyRenewalWindowDays": 7
    },
    "plans": [
      {
        "planId": "d1000000-0000-4000-8000-000000000001",
        "productType": "MONTHLY",
        "status": "PUBLISHED",
        "isEnabled": true,
        "hasDraft": false,
        "activeRevision": 2,
        "entitlementType": "WHEN_SPACE_AVAILABLE",
        "zoneId": null,
        "vehicleTypes": ["CAR", "MOTORCYCLE"],
        "ordinaryCoverageRules": {
          "dayCovered": true,
          "nightCovered": true,
          "excludedZoneIds": []
        },
        "financialReadiness": {
          "hasCompleteFinancialTerms": true,
          "financialTermId": "f1000000-0000-4000-8000-000000000001",
          "financialTermRevision": 1,
          "blockedReason": null
        },
        "updatedAt": "2026-11-17T03:00:00Z"
      },
      {
        "planId": null,
        "productType": "YEARLY",
        "status": "UNCONFIGURED",
        "isEnabled": false,
        "hasDraft": false,
        "activeRevision": 0,
        "entitlementType": "WHEN_SPACE_AVAILABLE",
        "zoneId": null,
        "vehicleTypes": [],
        "ordinaryCoverageRules": null,
        "financialReadiness": {
          "hasCompleteFinancialTerms": false,
          "financialTermId": null,
          "financialTermRevision": null,
          "blockedReason": "YEARLY_POLICY_BLOCKED_Q05"
        },
        "updatedAt": null
      }
    ]
  }
}
```

#### Validation and error responses

| HTTP status | Machine code | Trigger | Client recovery |
| --- | --- | --- | --- |
| `401` | `UNAUTHENTICATED` | Thiếu hoặc JWT không hợp lệ | Đăng nhập lại |
| `403` | `FORBIDDEN_SITE_SCOPE` | Owner không sở hữu `siteId` | Kiểm tra lại `siteId` |
| `404` | `SITE_NOT_FOUND` | Không tìm thấy `siteId` | Kiểm tra `siteId` |

---

### A2. Lưu nháp định nghĩa gói (`OperationService`)

| Item | Contract |
| --- | --- |
| Method / URL | `PUT /api/v1/lots/{siteId}/pass-plans/{productType}/draft` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

Path: `productType` ∈ `{"MONTHLY", "YEARLY"}` (case-insensitive normalized to uppercase).

```json
{
  "zoneId": null,
  "vehicleTypes": ["CAR", "MOTORCYCLE"],
  "ordinaryCoverageRules": {
    "dayCovered": true,
    "nightCovered": true,
    "excludedZoneIds": []
  },
  "isAvailableOnly": true
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Bãi xe của Owner | `b0000000-...` |
| `productType` | path | string enum | Required / Non-null | `MONTHLY` hoặc `YEARLY` | `MONTHLY` |
| `zoneId` | body | UUID v4 | Optional / Nullable | `null` = áp dụng toàn bãi; non-null = zone thuộc `siteId` | `null` |
| `vehicleTypes` | body | string[] | Required / Non-empty | Phần tử ∈ `CAR`, `MOTORCYCLE`, `OVERSIZED`, thuộc tập xe bãi hỗ trợ | `["CAR"]` |
| `ordinaryCoverageRules` | body | object | Required / Non-null | Phủ sóng ngày/đêm thường nhật (`dayCovered` \| `nightCovered` ít nhất 1 `true`) | `{"dayCovered":true,"nightCovered":true}` |
| `isAvailableOnly` | body | boolean | Required / Non-null | Bắt buộc phải là `true` (`WHEN_SPACE_AVAILABLE`); gửi `false` bị từ chối | `true` |

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "planId": "d1000000-0000-4000-8000-000000000001",
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "productType": "MONTHLY",
    "status": "DRAFT",
    "targetRevision": 3,
    "updatedAt": "2026-11-17T03:05:00Z"
  }
}
```

#### Validation and error responses

| HTTP status | Machine code | Trigger | Client recovery |
| --- | --- | --- | --- |
| `400` | `VALIDATION_ERROR` | `vehicleTypes` rỗng, `dayCovered` & `nightCovered` đều `false`, hoặc `isAvailableOnly != true` | Sửa payload theo thông báo lỗi chi tiết |
| `422` | `UNSUPPORTED_LOT_SCOPE` | `zoneId` không thuộc `siteId` hoặc `vehicleTypes` không được bãi hỗ trợ | Kiểm tra cấu hình Facility của bãi |

---

### A3. Xuất bản phiên bản gói (`OperationService`)

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/v1/lots/{siteId}/pass-plans/{productType}/publish` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | `application/json` |
| Headers | `Authorization: Bearer <token>`, `Idempotency-Key: <string>` (Optional/Recommended, tối đa 128 ký tự; nếu replay trả header `Idempotency-Replayed: true` giống `LotPoliciesController`) |

#### Request sample

```json
{
  "publishNote": "Áp dụng phạm vi xe ô tô & xe máy khu A"
}
```

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "planId": "d1000000-0000-4000-8000-000000000001",
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "productType": "MONTHLY",
    "publishedRevision": 3,
    "effectiveFrom": "2026-11-17T03:10:00Z",
    "isEnabled": false,
    "isReplayed": false
  }
}
```

> Lưu ý: Publish chỉ khóa định nghĩa kỹ thuật (`planRevision`) và **không tự động bật bán** (`isEnabled` giữ nguyên trạng thái hiện tại hoặc `false` nếu mới tạo). Các kỳ đã thanh toán trước đó vẫn giữ nguyên snapshot revision cũ (`M-06`).

#### Retry and state outcomes

| Case | Observable contract |
| --- | --- |
| Repeated request với cùng `Idempotency-Key` & cùng payload | Trả `200 OK` với kết quả cũ và header `Idempotency-Replayed: true` |
| Repeated request với cùng `Idempotency-Key` nhưng khác payload | Trả `409 IDEMPOTENCY_CONFLICT` |
| Chưa có bản draft / chưa có cấu hình | Trả `404 PASS_PLAN_DRAFT_NOT_FOUND` |

---

### A4. Bật / tắt bán gói độc lập (`OperationService`)

| Item | Contract |
| --- | --- |
| Method / URL | `PATCH /api/v1/lots/{siteId}/pass-plans/{productType}/enablement` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

```json
{
  "isEnabled": true,
  "expectedPlanRevision": 3
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `isEnabled` | body | boolean | Required / Non-null | `true` = mở bán cho người mua mới; `false` = ngừng bán mới (giữ nguyên kỳ đã trả tiền) | `true` |
| `expectedPlanRevision` | body | integer | Optional / Nullable | Optimistic concurrency guard: nếu truyền và khác `activeRevision`, trả `409` | `3` |

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "planId": "d1000000-0000-4000-8000-000000000001",
    "productType": "MONTHLY",
    "activeRevision": 3,
    "isEnabled": true,
    "updatedAt": "2026-11-17T03:12:00Z"
  }
}
```

#### Validation and error responses

| HTTP status | Machine code | Trigger | Client recovery |
| --- | --- | --- | --- |
| `409` | `PLAN_VERSION_CONFLICT` | `expectedPlanRevision` không khớp `activeRevision` | Tải lại plan rồi thử lại |
| `422` | `PLAN_NOT_PUBLISHED` | Cố bật `isEnabled: true` khi chưa publish bản nào | Gọi `publish` trước |
| `422` | `MISSING_FINANCIAL_TERMS` | `PaymentService` (gọi qua S2S Service JWT) báo chưa có đủ giá & điều khoản hoàn tiền active cho gói | Cấu hình giá bên `PaymentService` (A5) trước khi bật |
| `422` | `YEARLY_POLICY_BLOCKED_Q05` | Cố bật `isEnabled: true` cho `productType = YEARLY` khi Q05 chưa duyệt | Chờ phê duyệt `V11-DES-05` / `Q05` |

---

### A5. Cấu hình giá trọn kỳ & điều khoản hoàn tiền (`PaymentService`)

| Item | Contract |
| --- | --- |
| Method / URL | `PUT /api/v1/pricing/lots/{siteId}/pass-plans/{planId}/financial-terms` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` (kiểm tra Role `OWNER` + quyền sở hữu `siteId`) |
| Permission / account state | Role `OWNER`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | `application/json` |
| Headers | `Authorization: Bearer <token>`, `Idempotency-Key: <string>` (Optional) |

#### Request sample

```json
{
  "productType": "MONTHLY",
  "planRevision": 3,
  "currency": "VND",
  "vehiclePrices": [
    {
      "vehicleType": "CAR",
      "fullPeriodPrice": 1500000
    },
    {
      "vehicleType": "MOTORCYCLE",
      "fullPeriodPrice": 180000
    }
  ],
  "refundPolicy": {
    "voluntaryBeforeStartPercent": 100,
    "voluntaryInPeriodPercent": 50,
    "facilityClosurePercent": 100,
    "roundingMode": "HALF_UP_THOUSAND"
  }
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `productType` | body | string enum | Required / Non-null | `MONTHLY` hoặc `YEARLY` (hiện `YEARLY` bị chặn bởi Q05) | `MONTHLY` |
| `planRevision` | body | integer | Required / Non-null | Revision gói bên `OperationService` mà điều khoản tài chính này gắn vào (`>= 1`) | `3` |
| `currency` | body | string | Required / Non-null | Chỉ hỗ trợ `"VND"` trong MVP | `VND` |
| `vehiclePrices` | body | array | Required / Non-empty | Giá trọn kỳ (`fullPeriodPrice > 0`, bội số VND hợp lệ; **không có default**) cho từng `vehicleType` của gói | `[{"vehicleType":"CAR","fullPeriodPrice":1500000}]` |
| `refundPolicy` | body | object | Required / Non-null | Với `MONTHLY` theo `M-05`/`M-06`: trước S 100%, trong `[S,E)` 50%, đóng bãi 100%, làm tròn `HALF_UP_THOUSAND`. Không copy sang `YEARLY` | — |

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "financialTermId": "f1000000-0000-4000-8000-000000000001",
    "planId": "d1000000-0000-4000-8000-000000000001",
    "planRevision": 3,
    "financialTermRevision": 1,
    "productType": "MONTHLY",
    "currency": "VND",
    "vehiclePrices": [
      { "vehicleType": "CAR", "fullPeriodPrice": 1500000 },
      { "vehicleType": "MOTORCYCLE", "fullPeriodPrice": 180000 }
    ],
    "effectiveFrom": "2026-11-17T03:11:00Z"
  }
}
```

#### Validation and error responses

| HTTP status | Machine code (`code`) | Trigger | Client recovery |
| --- | --- | --- | --- |
| `400` | `INVALID_FINANCIAL_TERMS` | `fullPeriodPrice <= 0`, trùng `vehicleType`, hoặc thiếu giá cho loại xe | Nhập đầy đủ giá hợp lệ (`> 0`) |
| `422` | `YEARLY_POLICY_BLOCKED_Q05` | `productType == YEARLY` khi chính sách Q05 chưa duyệt | Giữ trạng thái blocked cho Yearly |

---

### A6. Báo cáo thu / hoàn tiền gói cho Owner (`PaymentService`)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/payments/owners/lots/{siteId}/pass-revenue-summary` |
| Availability | Proposed (`SPARK-331` / `SPARK-341`) |
| Authentication | Bearer JWT |
| Freshness / invocation origin | `TOKEN` |
| Permission / account state | Role `OWNER`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Query params | `from` (ISO 8601, optional), `to` (ISO 8601, optional), `productType` (`MONTHLY` \| `YEARLY`, optional) |

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "currency": "VND",
    "collectedAmount": 4500000,
    "collectedGrantedAmount": 3000000,
    "collectedAwaitingRefundAmount": 1500000,
    "pendingRefundAmount": 1500000,
    "refundedAmount": 750000,
    "netSettledRevenue": 2250000,
    "counts": {
      "fulfilledPasses": 2,
      "awaitingRefundOrders": 1,
      "completedRefunds": 1
    }
  }
}
```

> Đáp ứng `FR-MON-13`: Tách biệt rõ tiền đã thu (`collectedGrantedAmount`) với tiền đã thu nhưng đang chờ hoàn (`collectedAwaitingRefundAmount` / `pendingRefundAmount`), tuyệt đối không hiển thị tiền chờ hoàn như gói đã cấp thành công.

---

## API Specification — Phần B: Contracts bàn giao cho `SPARK-332`

> [!IMPORTANT]
> Đây là các contract trọng yếu để team `SPARK-332` (`SPARK-344`, `SPARK-345`, `SPARK-346`) tích hợp trực tiếp. Mọi giao tiếp HTTP S2S (`B3`, `B4`, tạo `PaymentOrder`) đều dùng **Signed RS256 Service JWT** theo đúng mẫu `OperationsServiceAuthentication` trên `origin/develop`.

### B1. Driver xem danh sách gói khả dụng của bãi (`OperationService` → gọi `PaymentService` qua S2S JWT)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/sites/{siteId}/pass-offers` |
| Availability | Proposed (`SPARK-331` delivers · `SPARK-332` FE/BE consumes) |
| Authentication | Anonymous hoặc Bearer JWT |
| Freshness / invocation origin | `PUBLIC` (theo [authorization-operation-classification.md](../../01-requirements/srs/authorization-operation-classification.md)) |
| Permission / account state | Public catalogue (chỉ trả gói `isEnabled = true` và đủ financial terms) |
| Resource scope | Public site catalogue (`siteId`) |
| Query params | `vehicleType` (optional: `CAR` \| `MOTORCYCLE` \| `OVERSIZED`) |

#### Request sample

`GET /api/v1/sites/b0000000-0000-4000-8000-000000000001/pass-offers?vehicleType=CAR`

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "siteId": "b0000000-0000-4000-8000-000000000001",
    "timeZoneId": "Asia/Ho_Chi_Minh",
    "yearlyBlockedByQ05": true,
    "offers": [
      {
        "planId": "d1000000-0000-4000-8000-000000000001",
        "planRevision": 3,
        "financialTermId": "f1000000-0000-4000-8000-000000000001",
        "financialTermRevision": 1,
        "productType": "MONTHLY",
        "entitlementType": "WHEN_SPACE_AVAILABLE",
        "guaranteedCapacity": false,
        "zoneId": null,
        "vehicleType": "CAR",
        "fullPeriodPrice": 1500000,
        "currency": "VND",
        "ordinaryCoverageRules": {
          "dayCovered": true,
          "nightCovered": true,
          "excludedZoneIds": []
        },
        "effectivePolicyParams": {
          "maxPrepaidTerms": 3,
          "paymentHoldMinutes": 15,
          "renewalWindowDays": 7
        },
        "anchorRule": "CALENDAR_MONTH_PRESERVE_ANCHOR_CLAMP_END",
        "refundPolicySummary": {
          "voluntaryBeforeStartPercent": 100,
          "voluntaryInPeriodPercent": 50,
          "facilityClosurePercent": 100,
          "roundingMode": "HALF_UP_THOUSAND"
        }
      }
    ]
  }
}
```

> **Lưu ý cho FE `SPARK-345`:**
> - Nếu `offers` rỗng (bãi chưa bật gói hoặc thiếu giá), trả `200 OK` với `"offers": []` (không trả `404`), FE hiển thị trạng thái chưa mở bán.
> - FE phải gửi lại cặp `(planId, planRevision, financialTermId, financialTermRevision)` Driver vừa review lên API tạo đơn của `SPARK-332` để Backend phát hiện ngay nếu Owner vừa sửa điều khoản.

---

### B2. Validate & Freeze Plan Snapshot (`OperationService` In-Process Port cho `332`)

| Item | Contract |
| --- | --- |
| Primary Integration | **In-Process Application Port** (`IPassPlanSnapshotService.ValidateAndFreezeAsync(...)`) trong `OperationService.Application` |
| Optional HTTP Route (chỉ dùng nếu cần QA/test độc lập) | `POST /internal/pass-plans/validate-and-freeze` |
| Authentication (nếu mở HTTP) | **Signed RS256 Service JWT** (`Authorization: Bearer <service JWT>`, `aud = SmartParking.OperationService`, `permission = pass:plan:freeze`) — **tuyệt đối không dùng `X-Service-Key`** |
| Freshness / invocation origin | `SERVICE` (hoặc in-process Application call trong cùng transaction tạo đơn của `OperationService`) |

#### Input & Output Contract (dùng chung cho C# Interface & HTTP DTO nếu mở)

**Input:**

```json
{
  "siteId": "b0000000-0000-4000-8000-000000000001",
  "planId": "d1000000-0000-4000-8000-000000000001",
  "expectedPlanRevision": 3,
  "productType": "MONTHLY",
  "vehicleType": "CAR"
}
```

**Output (Frozen `PlanSnapshot`):**

```json
{
  "planId": "d1000000-0000-4000-8000-000000000001",
  "planRevision": 3,
  "productType": "MONTHLY",
  "entitlementType": "WHEN_SPACE_AVAILABLE",
  "siteId": "b0000000-0000-4000-8000-000000000001",
  "zoneId": null,
  "vehicleType": "CAR",
  "timeZoneId": "Asia/Ho_Chi_Minh",
  "ordinaryCoverageRules": {
    "dayCovered": true,
    "nightCovered": true,
    "excludedZoneIds": []
  },
  "effectivePolicyParams": {
    "maxPrepaidTerms": 3,
    "paymentHoldMinutes": 15,
    "renewalWindowDays": 7
  }
}
```

#### Domain / HTTP Error Outcomes

| HTTP status (nếu qua HTTP) | Machine code | Trigger | Caller (`332`) recovery |
| --- | --- | --- | --- |
| `404` | `PASS_PLAN_NOT_FOUND` | Không tồn tại `planId` tại `siteId` | Không tạo đơn, báo gói không tồn tại |
| `409` | `PLAN_VERSION_CHANGED` | Owner đã publish revision mới khác `expectedPlanRevision` | Trả về FE yêu cầu Driver review lại offer mới (`GET /pass-offers`) |
| `422` | `PLAN_NOT_PURCHASABLE` | Gói đang `isEnabled = false` hoặc `vehicleType` không nằm trong phạm vi | Chặn tạo đơn, thông báo gói tạm ngừng bán |
| `422` | `YEARLY_POLICY_BLOCKED_Q05` | `productType == YEARLY` | Chặn tạo đơn |

---

### B3. Internal Financial Terms Lookup & Quote Snapshot (`PaymentService` — S2S Service JWT)

| Item | Contract |
| --- | --- |
| Method / URL 1 (Readiness / Offers) | `GET /internal/pass-financial-terms?siteId={siteId}&planId={planId}` |
| Method / URL 2 (Freeze Quote for 332) | `POST /internal/pass-financial-terms/quote-snapshot` |
| Availability | Proposed (`SPARK-331` delivers · `OperationService` & `SPARK-332` call) |
| Authentication | **Signed RS256 Service JWT** (`Authorization: Bearer <service JWT>`, scheme `OperationsService`, tham chiếu `OperationsServiceAuthentication.cs` trên `origin/develop`) |
| Required JWT Claims | `iss = "SmartParking.OperationService"`, `sub = "SmartParking.OperationService"`, `sender = "SmartParking.OperationService"`, `aud = "SmartParking.PaymentService"`, `exp` (`TTL = 60s`, `clockSkew <= 5s`), `permission` ∈ `{ "pass:financial-terms:read", "pass:financial-terms:quote" }` |
| Freshness / invocation origin | `SERVICE` (từ chối Driver JWT và `X-Service-Key`) |
| Content type | `application/json` |

#### Request sample (`POST /internal/pass-financial-terms/quote-snapshot`)

```json
{
  "siteId": "b0000000-0000-4000-8000-000000000001",
  "planId": "d1000000-0000-4000-8000-000000000001",
  "planRevision": 3,
  "expectedFinancialTermRevision": 1,
  "productType": "MONTHLY",
  "vehicleType": "CAR"
}
```

#### Response sample (`200 OK`)

```json
{
  "success": true,
  "data": {
    "financialTermId": "f1000000-0000-4000-8000-000000000001",
    "financialTermRevision": 1,
    "planId": "d1000000-0000-4000-8000-000000000001",
    "planRevision": 3,
    "productType": "MONTHLY",
    "vehicleType": "CAR",
    "fullPeriodPrice": 1500000,
    "currency": "VND",
    "refundPolicySnapshot": {
      "voluntaryBeforeStartPercent": 100,
      "voluntaryInPeriodPercent": 50,
      "facilityClosurePercent": 100,
      "roundingMode": "HALF_UP_THOUSAND"
    },
    "quotedAt": "2026-11-17T03:15:00Z"
  }
}
```

#### Validation and error responses (`PaymentService` envelope)

| HTTP status | Machine code (`code`) | Trigger | Caller (`332`) recovery |
| --- | --- | --- | --- |
| `401` | `UNAUTHENTICATED` | Thiếu, hết hạn, sai chữ ký RS256, hoặc dùng `X-Service-Key` / Driver JWT | Kiểm tra cấu hình RSA key của `OperationService` |
| `403` | `FORBIDDEN` | Sai `iss` / `sub` / `sender` / `aud` / thiếu claim `permission` phù hợp | Kiểm tra policy & claims cấp cho `OperationService` |
| `409` | `FINANCIAL_TERMS_CHANGED` | Giá/điều khoản đã cập nhật lên revision mới | Báo FE ép Driver review lại giá mới |
| `422` | `FINANCIAL_TERMS_MISSING` | Chưa có giá active cho `(planId, vehicleType)` | Chặn mua |
| `422` | `YEARLY_POLICY_BLOCKED_Q05` | `productType == YEARLY` | Chặn mua |

---

### B4. Internal Closure Compensation (`PaymentService` — `M-06` Idempotent Closure Refund)

| Item | Contract |
| --- | --- |
| Method / URL | `POST /internal/pass-closures/compensate` (hoặc async consumer từ sự kiện `CoverageUnavailableVerified`) |
| Availability | Proposed (`SPARK-331` / `M-06`) |
| Authentication | **Signed RS256 Service JWT** (`Authorization: Bearer <service JWT>`, `iss/sub/sender = SmartParking.OperationService`, `aud = SmartParking.PaymentService`, `permission = pass:closures:compensate`) |
| Freshness / invocation origin | `SERVICE` |

- **Mục đích:** Khi `FacilityService` / `OperationService` ghi nhận đóng cửa tạm thời hoặc vĩnh viễn đã xác thực trên các khoảng thời gian có gói đã thanh toán, gửi danh sách khoảng mất hiệu lực `(closureId, closureType: "TEMPORARY" | "PERMANENT", passId, paymentOrderId, paidPeriodStart, paidPeriodEnd, unavailableIntervals[])` kèm `idempotencyKey`.
- **Invariants bắt buộc:**
  - `TEMPORARY`: giữ nguyên `valid_until`, hợp nhất (`union`) các khoảng gián đoạn mới, **trừ phần giao đã hoàn trước đó** (`subscription_closure_compensations`), tính tiền hoàn theo tỷ lệ thời gian và làm tròn `HALF_UP_THOUSAND` một lần.
  - `PERMANENT`: hủy phần còn lại, hoàn `100%` giá trị phần chưa dùng còn lại (đã trừ overlap đã bồi thường trước đó).
  - Không bao giờ hoàn vượt quá số tiền thực thu còn lại của `payment_order_id`.

---

### B5. Ghi chú Tái sử dụng Luồng Thanh toán payOS có sẵn cho `SPARK-332`

`SPARK-331` **không** sửa đổi cổng payOS; team `SPARK-332` tái sử dụng toàn bộ hạ tầng payOS đã có trong `PaymentService`:
1. **Tạo `PaymentOrder` (`PENDING`, `order_type = MONTHLY_PASS_FEE`, `expiresAt = H`):**
   - `OperationService` (`SPARK-344`) gọi internal endpoint bên `PaymentService` bằng **Signed RS256 Service JWT** (áp dụng H-fence `now < H` giống `EnsureReservationDepositOrderCommandHandler.cs` trên `origin/develop`).
2. **Khởi tạo payOS VietQR (ĐÃ CÓ SẴN):**
   - FE (`SPARK-345`) gọi `POST /api/v1/payments/orders/{orderId}/checkout` (`PaymentsController.cs`, body `{"paymentMethod": "PAYOS", "returnUrl": "...", "cancelUrl": "..."}`, kèm `Idempotency-Key`) để nhận `checkoutUrl` & `qrCode` từ `PayOsAdapter`.
   - Môi trường dev có sẵn `POST /api/v1/payments/orders/{orderId}/demo-pay`.
3. **Nhận kết quả & Đối soát (ĐÃ CÓ SẴN):**
   - Webhook IPN: `POST /api/v1/payments/callbacks/payos` (hoặc `/api/v1/payments/payos/webhook`).
   - Kiểm tra trạng thái / đối soát: `GET /api/v1/payments/transactions/{transactionId}/status`.
   - Hóa đơn PDF (`SPARK-321`): `GET /api/v1/payments/orders/{orderId}/invoice`.

---

## Database Schema Contract (`OperationsDb` & `PaymentDb`)

> [!NOTE]
> Phần DDL dưới đây là **Draft Schema Contract** tương thích trực tiếp với [03b-reservation-operations-service-db.sql](file:///d:/A-Van/Project/ojt/Mock-Project-Smart-Parking/scripts/database/microservices/03b-reservation-operations-service-db.sql) và [04-payment-pricing-service-db.sql](file:///d:/A-Van/Project/ojt/Mock-Project-Smart-Parking/scripts/database/microservices/04-payment-pricing-service-db.sql). Không sử dụng cross-DB foreign key; mọi liên kết giữa `OperationsDb` và `PaymentDb` dùng Logical UUID + Revision number.

### 1. `OperationsDb` (`smartpark_operations` schema)

```sql
-- [NEW - 331] Định nghĩa gói đỗ xe theo tháng/năm (tách biệt hoàn toàn với giá bên PaymentDb)
CREATE TABLE IF NOT EXISTS parking_pass_plans (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    entitlement_type VARCHAR(50) NOT NULL DEFAULT 'WHEN_SPACE_AVAILABLE'
        CHECK (entitlement_type = 'WHEN_SPACE_AVAILABLE'),
    is_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(30) NOT NULL DEFAULT 'DRAFT'
        CHECK (status IN ('DRAFT', 'PUBLISHED', 'DISABLED')),
    active_revision INT NOT NULL DEFAULT 0,
    draft_revision INT NULL,
    draft_payload JSONB NULL,
    created_by UUID NOT NULL,
    updated_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (site_id, product_type)
);

-- [NEW - 331] Snapshot bất biến mỗi lần Owner Publish gói (chỉ INSERT, không UPDATE)
CREATE TABLE IF NOT EXISTS parking_pass_plan_revisions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    plan_id UUID NOT NULL REFERENCES parking_pass_plans(id) ON DELETE RESTRICT,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    revision_number INT NOT NULL CHECK (revision_number >= 1),
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    zone_id UUID NULL,
    vehicle_types JSONB NOT NULL, -- VD: ["CAR", "MOTORCYCLE"]
    ordinary_coverage_rules JSONB NOT NULL, -- VD: {"dayCovered": true, "nightCovered": true, "excludedZoneIds": []}
    contract_timezone VARCHAR(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_to TIMESTAMPTZ NULL,
    publish_note TEXT NULL,
    idempotency_key VARCHAR(128) NULL,
    idempotency_payload_hash VARCHAR(128) NULL,
    published_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (plan_id, revision_number)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_pass_plan_rev_idempotency
    ON parking_pass_plan_revisions(plan_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
```

**Ghi chú căn chỉnh bảng `monthly_passes` hiện có cho `SPARK-332` (`DB28`):**
Bảng `monthly_passes` hiện tại ở dòng 443 của `03b-reservation-operations-service-db.sql` đã có `CHECK (entitlement_type = 'WHEN_SPACE_AVAILABLE')` và `no_overlapping_passes_per_vehicle`. Để `332` lưu đầy đủ snapshot từ `331` theo `M-01..M-03`, cần bổ sung các cột:

```sql
-- [ALIGNMENT - 332 consumes 331] Các cột mở rộng đề xuất cho bảng monthly_passes
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS plan_id UUID NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS plan_revision INT NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS product_type VARCHAR(20) NOT NULL DEFAULT 'MONTHLY'
    CHECK (product_type IN ('MONTHLY', 'YEARLY'));
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS zone_id UUID NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS original_anchor_day SMALLINT NULL CHECK (original_anchor_day BETWEEN 1 AND 31);
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS original_anchor_time TIME NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS contract_timezone VARCHAR(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh';
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS financial_term_id UUID NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS financial_term_revision INT NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS accepted_price DECIMAL(14,2) NULL CHECK (accepted_price >= 0);
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS payment_order_id UUID NULL;
ALTER TABLE monthly_passes ADD COLUMN IF NOT EXISTS authorization_workflow_id UUID NULL;
-- Lưu ý cập nhật CHECK constraint status để hỗ trợ 'SCHEDULED' (kỳ mua trước chưa tới ngày S)
```

---

### 2. `PaymentDb`

```sql
-- [NEW - 331] Cấu hình giá trọn kỳ & điều khoản hoàn tiền gắn với (plan_id, plan_revision)
-- Mỗi lần Owner chỉnh giá/refund sinh 1 revision mới bất biến (không UPDATE đè bản đã bán)
CREATE TABLE IF NOT EXISTS subscription_financial_terms (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    plan_id UUID NOT NULL, -- Logical ID trỏ sang OperationsDb.parking_pass_plans.id
    product_type VARCHAR(20) NOT NULL CHECK (product_type IN ('MONTHLY', 'YEARLY')),
    active_revision INT NOT NULL DEFAULT 1,
    currency VARCHAR(10) NOT NULL DEFAULT 'VND',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (site_id, plan_id)
);

CREATE TABLE IF NOT EXISTS subscription_financial_term_revisions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    financial_term_id UUID NOT NULL REFERENCES subscription_financial_terms(id) ON DELETE RESTRICT,
    plan_id UUID NOT NULL,
    plan_revision INT NOT NULL CHECK (plan_revision >= 1),
    revision_number INT NOT NULL CHECK (revision_number >= 1),
    vehicle_prices JSONB NOT NULL, -- VD: [{"vehicleType":"CAR","fullPeriodPrice":1500000}]
    refund_policy JSONB NOT NULL,  -- VD: {"voluntaryBeforeStartPercent":100,"voluntaryInPeriodPercent":50,"facilityClosurePercent":100,"roundingMode":"HALF_UP_THOUSAND"}
    effective_from TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_to TIMESTAMPTZ NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (financial_term_id, revision_number)
);
CREATE INDEX IF NOT EXISTS idx_sub_fin_rev_plan
    ON subscription_financial_term_revisions(plan_id, plan_revision, effective_from DESC);

-- [NEW - 331] Chống hoàn tiền trùng lặp (double-refund) khi đóng cửa tạm thời / vĩnh viễn (M-06)
CREATE TABLE IF NOT EXISTS subscription_closure_compensations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    closure_event_id UUID NOT NULL,
    closure_type VARCHAR(20) NOT NULL CHECK (closure_type IN ('TEMPORARY', 'PERMANENT')),
    pass_id UUID NOT NULL, -- Logical ID sang OperationsDb.monthly_passes.id
    payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT,
    refund_request_id UUID NULL REFERENCES refund_requests(id) ON DELETE RESTRICT,
    compensated_from TIMESTAMPTZ NOT NULL,
    compensated_to TIMESTAMPTZ NOT NULL,
    refund_amount DECIMAL(14,2) NOT NULL CHECK (refund_amount >= 0),
    idempotency_key VARCHAR(128) NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (compensated_to > compensated_from)
);
```

> Ghi chú tương thích với bảng có sẵn trong `04-payment-pricing-service-db.sql`:
> - `policy_revisions` đã có sẵn `policy_type = 'SUBSCRIPTION'`: khi ghi `subscription_financial_term_revisions`, có thể đồng bộ ghi kèm vào `policy_revisions` nếu cần tra cứu tập trung theo `PolicyRevision` entity hiện hữu.
> - `payment_orders` và `invoices` đã hỗ trợ sẵn `order_type = 'MONTHLY_PASS_FEE'` và `reference_type = 'MONTHLY_PASS'`.

---

## Open Questions and Deferred Items

| Item | Decision or implementation gap | Affected operation | Owner / Jira | Approval reference |
| --- | --- | --- | --- | --- |
| `Q05` / `V11-DES-05` | Chưa chốt thông số, tỷ lệ hoàn tiền, hạn thanh toán và quy tắc gia hạn riêng cho gói **Yearly** | `PATCH .../enablement`, `PUT .../financial-terms`, `GET .../pass-offers` (`YEARLY`) | Operations + Payment + BA / `SPARK-331`, `SPARK-332` | `BLOCKED` pending `V11-DES-05` |
| `DB28-PASS-EXT` | Bổ sung các cột `plan_id`, `plan_revision`, `original_anchor_*`, `SCHEDULED` trên `monthly_passes` chưa có migration SQL chính thức | `SPARK-332` (`SPARK-344`) | `SPARK-331` & `SPARK-332` BE | Pending `SPARK-341` / `SPARK-344` PR |

---

## Review Checks

- [x] Tuân thủ [api-design-template.md](api-design-template.md) và `V11-10` ownership (`OperationService` vs `PaymentService`).
- [x] Tuân thủ [SmartPark-Coding-Skill-v1.0.md §4](../../07-skills/SmartPark-Coding-Skill-v1.0.md): mọi S2S HTTP capability mới dùng **Signed RS256 Service JWT (`Authorization: Bearer`)** theo mẫu `OperationsServiceAuthentication`, đã loại bỏ hoàn toàn `X-Service-Key`.
- [x] Phân tách rõ API do `SPARK-331` triển khai cho Owner (`A1..A6`), port nội bộ `B2`, S2S contracts (`B3..B4`) và luồng payOS tái sử dụng (`B5`) cho `SPARK-332`.
- [x] Giữ nguyên các điểm bất biến từ `M-01..M-09` (available-only, tách enablement vs price, không default `1,200,000 VND`, block Yearly theo `Q05`, chống double-refund đóng bãi).
