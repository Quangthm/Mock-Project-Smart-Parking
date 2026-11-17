# Owner Configures Parking Lot Policies — API Design

| Metadata | Value |
| --- | --- |
| Status | APPROVED |
| Contract version / date | v1.0 / 2026-11-06 |
| Owning service / author | OperationService / AI Assistant |
| Jira design / implementation / verification | [SPARK-233](https://mangamanagement.atlassian.net/browse/SPARK-233) (Design) / [SPARK-230](https://mangamanagement.atlassian.net/browse/SPARK-230) (BE) / [SPARK-232](https://mangamanagement.atlassian.net/browse/SPARK-232) (Test) |
| Requirements baseline | SRS v1.1 §3.2.4–3.2.5; `BR-POL-01..04`; `policy-catalogue.md` (v1.1) |
| Implementation | NOT IMPLEMENTED (Target: SPARK-230) |

## Overview

Tài liệu này đặc tả chi tiết giao diện lập trình ứng dụng (API Specification) cho phép Chủ bãi xe (Owner) tra cứu danh mục chính sách vận hành của bãi xe, soạn thảo bản nháp ghi đè trong phạm vi cho phép của Admin, xuất bản kích hoạt chính sách và truy xuất lịch sử phiên bản để đối soát giao dịch.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | `FR-POL-01`, `FR-POL-03`, `FR-POL-04`, `FR-POL-05`, `FR-POL-07`, `FR-POL-08`; `BR-POL-01..04`; `UC-POL-01`; `US-OW02` |
| Permission / service ownership | `LOT_POLICY_MANAGE` · `OperationService` (Non-financial policies) / `PaymentService` (Tariff terms) |
| Approved decisions | V11-09 (Service ownership across setup form), V11-10 (Monthly/Yearly product flags), RA-06 (Numeric policy bounds) |

> [!IMPORTANT]
> **Ranh giới Dữ liệu & Nghiệp vụ (Data Ownership Boundary):**
> - `OperationService` chịu trách nhiệm toàn bộ các tham số vận hành: thời gian giữ chỗ (`hold duration`), thời gian đỗ tối thiểu/tối đa, độ trễ đến muộn, cửa sổ bảo vệ, thời gian điều phối trước, thời hạn chiếm chỗ, và cờ kích hoạt sản phẩm đỗ xe.
> - Bảng giá tài chính (`tariffs`), tỉ lệ hoàn tiền (`cancellation refund rates`), và phương thức đặt cọc thuộc quyền sở hữu của `PaymentService`. API này **tuyệt đối không** cấu hình bảng giá và **không** truy cập cross-DB sang `PaymentDb`.
> - `FR-POL-09` (Ngưỡng camera LPR) tiếp tục duy trì trạng thái **OPEN / Deferred**, không đưa vào baseline.

---

## API Specification

---

### Thao tác 1: Tra cứu Danh mục Chính sách & Giá trị Hiệu lực (Read Effective Policy Catalogue)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/lots/{siteId}/policies/catalogue` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` (Chủ sở hữu hợp pháp của bãi xe `siteId`) |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

No body.

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh duy nhất của bãi xe cần tra cứu | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |

#### Response sample

HTTP 200 OK.

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "activeRevision": 3,
    "activeSince": "2026-10-15T08:00:00Z",
    "hasDraft": true,
    "policies": [
      {
        "key": "PAYMENT_HOLD_DURATION",
        "category": "RESERVATION_WINDOW",
        "dataType": "integer",
        "unit": "minutes",
        "adminDefault": 15,
        "adminBounds": {
          "min": 5,
          "max": 30
        },
        "configuredValue": 10,
        "effectiveValue": 10,
        "source": "OWNER_OVERRIDE",
        "isConfigurable": true
      },
      {
        "key": "ARRIVAL_LATE_TOLERANCE",
        "category": "CHECKIN_TOLERANCE",
        "dataType": "integer",
        "unit": "minutes",
        "adminDefault": 15,
        "adminBounds": {
          "min": 15,
          "max": 30
        },
        "configuredValue": null,
        "effectiveValue": 15,
        "source": "ADMIN_DEFAULT",
        "isConfigurable": true
      },
      {
        "key": "PARKING_PRODUCT_ENABLED",
        "category": "PRODUCT_SUBSCRIPTION",
        "dataType": "object",
        "unit": "boolean_flags",
        "adminDefault": {
          "monthly": false,
          "yearly": false
        },
        "adminBounds": null,
        "configuredValue": {
          "monthly": true,
          "yearly": false
        },
        "effectiveValue": {
          "monthly": true,
          "yearly": false
        },
        "source": "OWNER_OVERRIDE",
        "isConfigurable": true
      }
    ]
  }
}
```

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| `siteId` | UUID | Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `activeRevision` | integer | Non-null | Số phiên bản chính sách đang hoạt động | `3` |
| `activeSince` | ISO-8601 UTC | Non-null | Thời điểm kích hoạt phiên bản hiện tại | `2026-10-15T08:00:00Z` |
| `hasDraft` | boolean | Non-null | Cờ báo hiệu bãi xe đang có bản nháp chưa xuất bản | `true` |
| `policies[].key` | string | Non-null | Mã định danh chính sách | `PAYMENT_HOLD_DURATION` |
| `policies[].category` | string | Non-null | Nhóm phân loại chính sách | `RESERVATION_WINDOW` |
| `policies[].dataType` | string | Non-null | Kiểu dữ liệu (`integer`, `object`, `string`) | `integer` |
| `policies[].unit` | string | Non-null | Đơn vị đo lường (`minutes`, `hours`, `days`) | `minutes` |
| `policies[].adminDefault` | any | Non-null | Giá trị mặc định của hệ thống | `15` |
| `policies[].adminBounds` | object | Nullable | Khoảng trần sàn `min`/`max` do Admin quy định | `{"min": 5, "max": 30}` |
| `policies[].configuredValue` | any | Nullable | Giá trị Owner cấu hình ghi đè (null nếu chưa cấu hình) | `10` |
| `policies[].effectiveValue` | any | Non-null | Giá trị thực tế áp dụng sau khi giải quyết độ ưu tiên | `10` |
| `policies[].source` | string | Non-null | Nguồn gốc giá trị hiệu lực (`ADMIN_DEFAULT` / `OWNER_OVERRIDE`) | `OWNER_OVERRIDE` |
| `policies[].isConfigurable` | boolean | Non-null | Owner có quyền chỉnh sửa trường này hay không | `true` |

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `401` | `UNAUTHORIZED` | Thiếu hoặc hết hạn JWT Bearer token | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại để lấy token mới |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Bãi xe thuộc quyền sở hữu của Owner khác | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác trên đúng bãi xe thuộc sở hữu |
| `404` | `SITE_NOT_FOUND` | `siteId` không tồn tại trong hệ thống | `{"success": false, "code": "SITE_NOT_FOUND", "message": "Parking lot not found."}` | Kiểm tra lại `siteId` |

#### Retry and state outcomes

Thao tác đọc idempotent hoàn toàn. Safe retry khi gặp lỗi mạng.

---

### Thao tác 2: Đọc Bản nháp Chính sách (Read Draft Policy Configuration)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/lots/{siteId}/policies/draft` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

No body.

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |

#### Response sample

HTTP 200 OK. *(Khi có bản nháp)*

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "status": "DRAFT",
    "targetRevision": 4,
    "updatedAt": "2026-11-06T09:30:00Z",
    "scheduledActivationAt": null,
    "policies": {
      "paymentHoldDurationMinutes": 10,
      "arrivalLateToleranceMinutes": 20,
      "minReservationDurationMinutes": 15,
      "maxReservationDurationHours": 24,
      "reservationProtectionWindowHours": 4,
      "allocationLeadTimeMinutes": 60,
      "occupancyValidityTimespanHours": 4,
      "driverReallocationRequestLimit": 2,
      "maxAdvanceBookingDays": 7,
      "interBookingBufferMinutes": 0,
      "parkingProductEnabled": {
        "monthly": true,
        "yearly": false
      }
    }
  }
}
```
*(Nếu chưa có bản nháp, trả về `{"success": true, "data": null}`).*

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| `siteId` | UUID | Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `status` | string | Non-null | Trạng thái bản nháp (`DRAFT`) | `DRAFT` |
| `targetRevision` | integer | Non-null | Số phiên bản dự kiến sau khi publish | `4` |
| `updatedAt` | ISO-8601 UTC | Non-null | Thời gian lưu nháp gần nhất | `2026-11-06T09:30:00Z` |
| `scheduledActivationAt` | ISO-8601 UTC | Nullable | Thời điểm hẹn giờ kích hoạt (nếu có) | `null` |
| `policies` | object | Non-null | Danh sách giá trị cấu hình trong bản nháp | Xem mẫu JSON |

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `401` | `UNAUTHORIZED` | Thiếu hoặc hết hạn token | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Không sở hữu bãi xe | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác đúng bãi |
| `404` | `SITE_NOT_FOUND` | Bãi xe không tồn tại | `{"success": false, "code": "SITE_NOT_FOUND", "message": "Parking lot not found."}` | Kiểm tra lại ID |

#### Retry and state outcomes

Thao tác đọc idempotent hoàn toàn.

---

### Thao tác 3: Cập nhật Bản nháp Chính sách (Upsert Draft Policy Configuration)

| Item | Contract |
| --- | --- |
| Method / URL | `PUT /api/v1/lots/{siteId}/policies/draft` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | Request & Response: `application/json` |
| Headers | `Authorization: Bearer <token>`, `Content-Type: application/json`, `Idempotency-Key: <UUID>` (Optional) |

#### Request sample

```json
{
  "policies": {
    "paymentHoldDurationMinutes": 10,
    "arrivalLateToleranceMinutes": 20,
    "minReservationDurationMinutes": 30,
    "maxReservationDurationHours": 48,
    "reservationProtectionWindowHours": 4,
    "allocationLeadTimeMinutes": 60,
    "occupancyValidityTimespanHours": 4,
    "driverReallocationRequestLimit": 3,
    "maxAdvanceBookingDays": 14,
    "interBookingBufferMinutes": 5,
    "parkingProductEnabled": {
      "monthly": true,
      "yearly": true
    }
  }
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `policies.paymentHoldDurationMinutes` | body | integer | Optional / Non-null | Giới hạn Admin bounds: [5, 30] phút | `10` |
| `policies.arrivalLateToleranceMinutes` | body | integer | Optional / Nullable | Bounds: [15, 30] phút hoặc null (disabled) | `20` |
| `policies.minReservationDurationMinutes`| body | integer | Optional / Non-null | Bounds: [15, 60] phút. Phải $\le$ Max duration | `30` |
| `policies.maxReservationDurationHours` | body | integer | Optional / Non-null | Bounds: [1, 168] giờ (7 ngày) | `48` |
| `policies.reservationProtectionWindowHours`| body| integer | Optional / Non-null | Bounds: [1, 12] giờ | `4` |
| `policies.allocationLeadTimeMinutes` | body | integer | Optional / Non-null | Bounds: [15, 180] phút | `60` |
| `policies.occupancyValidityTimespanHours`| body | integer | Optional / Non-null | Bounds: [1, 8] giờ | `4` |
| `policies.driverReallocationRequestLimit`| body | integer | Optional / Non-null | Bounds: [0, 5] lần | `3` |
| `policies.maxAdvanceBookingDays` | body | integer | Optional / Non-null | Bounds: [1, 30] ngày | `14` |
| `policies.interBookingBufferMinutes` | body | integer | Optional / Non-null | Bounds: [0, 60] phút (0 là liền kề) | `5` |
| `policies.parkingProductEnabled` | body | object | Optional / Non-null | Cờ boolean bật/tắt gói đỗ tháng/năm | `{"monthly": true, "yearly": true}` |

#### Response sample

HTTP 200 OK.

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "status": "DRAFT",
    "targetRevision": 4,
    "updatedAt": "2026-11-06T10:15:00Z",
    "message": "Draft policy configuration saved successfully."
  }
}
```

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `400` | `POLICY_OUT_OF_BOUNDS` | Giá trị nằm ngoài giới hạn trần/sàn Admin | `{"success": false, "code": "POLICY_OUT_OF_BOUNDS", "message": "paymentHoldDurationMinutes must be between 5 and 30 minutes."}` | Chỉnh lại giá trị nằm trong khoảng `adminBounds` |
| `400` | `INVALID_TIME_DURATION` | `minReservationDuration` lớn hơn `maxReservationDuration` | `{"success": false, "code": "INVALID_TIME_DURATION", "message": "Min duration cannot exceed max duration."}` | Đảm bảo thời gian đỗ tối thiểu nhỏ hơn tối đa |
| `401` | `UNAUTHORIZED` | Token thiếu hoặc hết hạn | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Không sở hữu bãi xe | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác đúng bãi |
| `404` | `SITE_NOT_FOUND` | Bãi xe không tồn tại | `{"success": false, "code": "SITE_NOT_FOUND", "message": "Parking lot not found."}` | Kiểm tra lại ID |

#### Retry and state outcomes

Thao tác `PUT` ghi đè toàn bộ bản nháp, có tính chất Idempotent tự nhiên. Gửi lại request cùng payload cho ra cùng trạng thái lưu nháp.

---

### Thao tác 4: Xuất bản Kích hoạt Chính sách (Publish Policy Revision)

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/v1/lots/{siteId}/policies/publish` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | Request & Response: `application/json` |
| Headers | `Authorization: Bearer <token>`, `Content-Type: application/json`, `Idempotency-Key: <UUID>` (Bắt buộc) |

#### Request sample

```json
{
  "activationMode": "IMMEDIATE",
  "scheduledActivationAt": null,
  "publishNote": "Cập nhật tăng thời gian đặt trước lên 14 ngày và mở bán vé năm"
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `activationMode` | body | string enum | Required / Non-null | Giá trị: `IMMEDIATE` (kích hoạt ngay) hoặc `SCHEDULED` (hẹn giờ) | `IMMEDIATE` |
| `scheduledActivationAt` | body | ISO-8601 UTC | Conditional / Nullable | Bắt buộc khi `activationMode = SCHEDULED`. Phải là mốc thời gian tương lai | `null` |
| `publishNote` | body | string | Optional / Nullable | Ghi chú lý do thay đổi phiên bản (tối đa 500 ký tự) | `"Cập nhật mở bán vé năm"` |

#### Response sample

HTTP 200 OK.

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "status": "ACTIVE",
    "newRevision": 4,
    "activatedAt": "2026-11-06T10:20:00Z",
    "publishNote": "Cập nhật tăng thời gian đặt trước lên 14 ngày và mở bán vé năm"
  }
}
```

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| `siteId` | UUID | Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `status` | string | Non-null | Trạng thái sau xuất bản: `ACTIVE` hoặc `SCHEDULED` | `ACTIVE` |
| `newRevision` | integer | Non-null | Số phiên bản chính sách mới được ghi nhận | `4` |
| `activatedAt` | ISO-8601 UTC | Non-null | Thời điểm kích hoạt hiệu lực | `2026-11-06T10:20:00Z` |
| `publishNote` | string | Nullable | Ghi chú phiên bản | `"Cập nhật mở bán vé năm"` |

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `400` | `INVALID_ACTIVATION_SCHEDULE` | Hẹn giờ với mốc thời gian quá khứ | `{"success": false, "code": "INVALID_ACTIVATION_SCHEDULE", "message": "Scheduled time must be in the future."}` | Chọn lại thời điểm tương lai |
| `401` | `UNAUTHORIZED` | Token thiếu hoặc hết hạn | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Không sở hữu bãi xe | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác đúng bãi |
| `409` | `NO_DRAFT_TO_PUBLISH` | Không có bản nháp nào được lưu | `{"success": false, "code": "NO_DRAFT_TO_PUBLISH", "message": "No draft policy exists to publish."}` | Lưu bản nháp (`PUT /draft`) trước khi gọi publish |
| `409` | `IDEMPOTENCY_PAYLOAD_MISMATCH`| Dùng lại `Idempotency-Key` với payload khác | `{"success": false, "code": "IDEMPOTENCY_PAYLOAD_MISMATCH", "message": "Idempotency key payload mismatch."}` | Sinh key UUID v4 mới |
| `409` | `OPERATION_IN_PROGRESS` | Request cùng key đang được thực thi | `{"success": false, "code": "OPERATION_IN_PROGRESS", "message": "Another publish operation is in progress."}` | Chờ vài giây rồi thử lại |
| `422` | `UNPROCESSABLE_ENTITY` | Bản nháp thiếu tham số bắt buộc | `{"success": false, "code": "UNPROCESSABLE_ENTITY", "message": "Draft contains invalid configuration."}` | Điền đủ các trường bắt buộc |

#### Retry and state outcomes

| Case | Observable contract |
| --- | --- |
| Lặp lại request với cùng `Idempotency-Key` và payload | Trả về nguyên trạng Response 200 đã thành công trước đó kèm header `Idempotency-Replayed: true`. Không sinh thêm revision mới. |

---

### Thao tác 5: Danh sách Lịch sử các Phiên bản Chính sách (List Policy Revisions)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/lots/{siteId}/policies/revisions` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

No body.

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `page` | query | integer | Optional / Default: 1 | Số trang (tối thiểu 1) | `1` |
| `pageSize` | query | integer | Optional / Default: 10| Số phần tử trên trang (1 đến 50) | `10` |

#### Response sample

HTTP 200 OK.

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "totalRevisions": 4,
    "items": [
      {
        "revisionNumber": 4,
        "status": "ACTIVE",
        "effectiveFrom": "2026-11-06T10:20:00Z",
        "effectiveTo": null,
        "publishedBy": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "publishNote": "Cập nhật tăng thời gian đặt trước lên 14 ngày và mở bán vé năm"
      },
      {
        "revisionNumber": 3,
        "status": "ARCHIVED",
        "effectiveFrom": "2026-10-15T08:00:00Z",
        "effectiveTo": "2026-11-06T10:20:00Z",
        "publishedBy": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "publishNote": "Cấu hình ban đầu cho bãi xe"
      }
    ]
  }
}
```

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| `totalRevisions` | integer | Non-null | Tổng số phiên bản đã từng ghi nhận | `4` |
| `items[].revisionNumber` | integer | Non-null | Số thứ tự phiên bản | `4` |
| `items[].status` | string | Non-null | `ACTIVE`, `SCHEDULED`, hoặc `ARCHIVED` | `ACTIVE` |
| `items[].effectiveFrom` | ISO-8601 UTC | Non-null | Thời điểm bắt đầu có hiệu lực | `2026-11-06T10:20:00Z` |
| `items[].effectiveTo` | ISO-8601 UTC | Nullable | Thời điểm kết thúc hiệu lực (null nếu đang active) | `null` |
| `items[].publishedBy` | UUID | Non-null | Tài khoản Owner thực hiện publish | `3fa85f64-5717-4562-b3fc-2c963f66afa6` |
| `items[].publishNote` | string | Nullable | Ghi chú phiên bản | `"Cập nhật mở bán vé năm"` |

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `401` | `UNAUTHORIZED` | Token thiếu hoặc hết hạn | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Không sở hữu bãi xe | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác đúng bãi |
| `404` | `SITE_NOT_FOUND` | Bãi xe không tồn tại | `{"success": false, "code": "SITE_NOT_FOUND", "message": "Parking lot not found."}` | Kiểm tra lại ID |

#### Retry and state outcomes

Thao tác đọc danh sách có phân trang ổn định, sắp xếp giảm dần theo `revisionNumber`. Safe retry.

---

### Thao tác 6: Tra cứu Snapshot Phiên bản Quá khứ (Read Historical Revision Snapshot)

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/v1/lots/{siteId}/policies/revisions/{version}` |
| Availability | Proposed for SPARK-230 |
| Authentication | Bearer JWT (RFC 6750) |
| Permission / account state | Role `OWNER`, Permission `LOT_POLICY_MANAGE`, tài khoản `ACTIVE` |
| Resource scope | `owned-lot scope` |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Request sample

No body.

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `siteId` | path | UUID v4 | Required / Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `version` | path | integer | Required / Non-null | Số thứ tự phiên bản cần tra cứu snapshot | `3` |

#### Response sample

HTTP 200 OK.

```json
{
  "success": true,
  "data": {
    "siteId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "revisionNumber": 3,
    "status": "ARCHIVED",
    "effectiveFrom": "2026-10-15T08:00:00Z",
    "effectiveTo": "2026-11-06T10:20:00Z",
    "publishedBy": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "policies": {
      "paymentHoldDurationMinutes": 15,
      "arrivalLateToleranceMinutes": 15,
      "minReservationDurationMinutes": 15,
      "maxReservationDurationHours": 24,
      "reservationProtectionWindowHours": 4,
      "allocationLeadTimeMinutes": 60,
      "occupancyValidityTimespanHours": 4,
      "driverReallocationRequestLimit": 2,
      "maxAdvanceBookingDays": 7,
      "interBookingBufferMinutes": 0,
      "parkingProductEnabled": {
        "monthly": false,
        "yearly": false
      }
    }
  }
}
```

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| `siteId` | UUID | Non-null | Định danh bãi xe | `7c9e6679-7425-40de-944b-e07fc1f90ae7` |
| `revisionNumber` | integer | Non-null | Số thứ tự phiên bản | `3` |
| `status` | string | Non-null | Trạng thái phiên bản tại thời điểm truy vấn | `ARCHIVED` |
| `effectiveFrom` | ISO-8601 UTC | Non-null | Thời điểm bắt đầu có hiệu lực | `2026-10-15T08:00:00Z` |
| `effectiveTo` | ISO-8601 UTC | Nullable | Thời điểm kết thúc hiệu lực | `2026-11-06T10:20:00Z` |
| `policies` | object | Non-null | Snapshot bất biến toàn bộ thông số chính sách tại phiên bản đó | Xem mẫu JSON |

#### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| `401` | `UNAUTHORIZED` | Token thiếu hoặc hết hạn | `{"success": false, "code": "UNAUTHORIZED", "message": "Authentication required."}` | Đăng nhập lại |
| `403` | `CROSS_LOT_ACCESS_DENIED` | Không sở hữu bãi xe | `{"success": false, "code": "CROSS_LOT_ACCESS_DENIED", "message": "You do not own this parking lot."}` | Thao tác đúng bãi |
| `404` | `REVISION_NOT_FOUND` | Số phiên bản không tồn tại | `{"success": false, "code": "REVISION_NOT_FOUND", "message": "Policy revision not found."}` | Tra cứu danh sách revision trước |

#### Retry and state outcomes

Dữ liệu snapshot trong quá khứ là **bất biến (Immutable)**. Mọi lượt gọi lặp lại luôn trả về kết quả đồng nhất.
