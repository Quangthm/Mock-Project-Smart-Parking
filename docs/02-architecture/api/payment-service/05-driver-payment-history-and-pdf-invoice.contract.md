# Lịch sử Thanh toán & Hóa đơn PDF Phía Máy chủ (Driver Payment/Refund History & Server-Side PDF Invoices)

> **Feature Key:** `SPARK-337` *(Kế thừa và hoàn thiện từ `SPARK-321`, `SPARK-247`, `SPARK-229` / `US-D06`)*  
> **Service Owner:** `Payment Service` *(Financial Domain — Quản lý Biểu giá, Dòng tiền, Đối soát và Hóa đơn Tài chính)*  
> **Target Interface / Route:**  
> - **Authoritative Customer Invoice PDF Download:** `GET /api/v1/payments/invoices/{invoiceId}/download`  
> - **Driver Invoice & Payment History (Paged & Filtered):** `GET /api/v1/payments/invoices/my-history`  
> **Document Revision:** `v1.0` *(Đặc tả chuẩn hóa Hợp đồng Hóa đơn PDF và Bộ lọc Lịch sử Thanh toán theo SRS v1.1)*  
> **API Version:** `v1` *(HTTP endpoints do SmartPark quản lý: `/api/v1/payments/...`)*  
> **Contract Status:** `Approved`  
> **Communication Type:** `Client-to-Backend HTTP REST`  
> **Author:** `AI Pair Programmer / Coding Team`  
> **Reviewer:** `Technical Lead / Independent Victory Auditor`  
> **SRS & Database Baseline:** `SRS v1.1 (FR-PAY-07), UC-PAY-02, BR-PAY-03, BR-PAY-04, BR-AUTH-03, 04-payment-pricing-service-db.sql`  

---

### Lịch sử thay đổi (Revision History / Change Log)

| Revision | Ngày | Tác giả | Người duyệt | Loại thay đổi | Mô tả thay đổi / Jira Key |
| :---: | :---: | :--- | :--- | :--- | :--- |
| `v1.0` | `2026-11-10` | `AI Pair Programmer` | `Victory Auditor` | Initial | Khởi tạo tài liệu đặc tả API Contract cho tính năng xuất file PDF hóa đơn máy chủ (Replay-safe PDF) và bộ lọc/phân trang lịch sử thanh toán theo ticket `SPARK-337`. |

---

## 1. Tổng quan & Ranh giới Nghiệp vụ (Overview & Business Boundaries)

### 1.1 Mục đích nghiệp vụ (Business Objective)
Tài liệu này định nghĩa **Hợp đồng Giao diện Lịch sử Thanh toán & Xuất Hóa đơn Điện tử PDF (Payment History & Server-Side PDF Invoice API Contract)** dành cho Tài xế (Driver) và Khách hàng (Customer) trong hệ thống SmartPark.

Hợp đồng giải quyết 4 yêu cầu nghiệp vụ bắt buộc theo **FR-PAY-07** và **UC-PAY-02**:
1. **Quyền tra cứu lịch sử tự phục vụ (Driver Self-Service History):** Cho phép tài xế xem danh sách các giao dịch/hóa đơn cá nhân có hỗ trợ phân trang (`pageNumber`, `pageSize`) và lọc theo trạng thái (`status`), khoảng thời gian (`fromDate`, `toDate`).
2. **Xuất hóa đơn PDF phía máy chủ bất biến (Server-Side Replay-safe PDF Invoices):** Máy chủ trực tiếp sinh tài liệu PDF chuẩn hóa từ snapshot giá chính thức (`QuestPDF`), đảm bảo client không thể tự can thiệp hay giả mạo số liệu tài chính.
3. **Rào chắn an toàn tài chính (Financial Settlement Guardrail):** Tuyệt đối **KHÔNG CHO PHÉP** xuất hóa đơn PDF cho các đơn hàng chưa thanh toán hoặc thanh toán thất bại (`PENDING`, `FAILED`). Chỉ những hóa đơn đã xác nhận dòng tiền thành công (`IsSettled == true` và `Status == "PAID"`) mới được phép cấp bản thể hiện PDF.
4. **Cách ly dữ liệu tài chính (Zero Cross-Account Leakage):** Nghiêm cấm truy vấn chéo dữ liệu giữa các tài xế. Mọi thao tác tải hóa đơn hay xem lịch sử đều được kiểm tra đối chiếu qua JWT Token của người gọi.

---

### 1.2 Phân định Quyền sở hữu Dữ liệu (Service Boundaries)

| Miền dữ liệu / Khái niệm | Service sở hữu | Trách nhiệm & Quyền hạn |
| :--- | :---: | :--- |
| **Hóa đơn tài chính & Bố cục PDF** | `PaymentService` | Nguồn sự thật duy nhất về dữ liệu hóa đơn, sinh file PDF nhị phân (`application/pdf`). |
| **Xác thực danh tính (Actor Context)** | `UserService` | Cung cấp định danh tài xế (`UserId`, `Role`) trong Claims của JWT Bearer Token. |
| **Phiên đỗ & Barie cổng ra** | `OperationService` | Ghi nhận xe ra vào vật lý (không sở hữu dữ liệu tài chính hay file PDF). |

---

### 1.3 Ma trận Truy vết Yêu cầu (Traceability Matrix)

| Mã yêu cầu | Phân loại | Nội dung nghiệp vụ | Mục hiện thực trong Contract |
| :--- | :---: | :--- | :--- |
| **SPARK-337** | Jira Story | Driver review payment/refund history and receive PDF invoices | Toàn bộ tài liệu |
| **FR-PAY-07** | Functional Req | Expose Driver's own payment/refund history and generate PDF invoice | Mục 2.1 & Mục 2.2 |
| **UC-PAY-02** | Use Case | Inspect invoices, payment history and charge explanations | Mục 2.1 & Mục 2.2 |
| **BR-PAY-03** | Business Rule | Authoritative payment status validation (no client inference) | Mục 2.1 (Settlement Guardrail) |
| **BR-PAY-04** | Business Rule | Issue replay-safe PDF invoices for settled financial obligations | Mục 2.1 (Idempotent PDF) |
| **BR-AUTH-03**| Business Rule | Account ownership verification and denial of cross-account access | Mục 1.1, Mục 2.1, Mục 2.2 |

---

## 2. Đặc tả Hợp đồng Giao diện HTTP REST API

---

### 2.1 API Tải Hóa đơn Điện tử PDF (Download Invoice PDF)

Endpoint cho phép tải về bản thể hiện file PDF chính thức của một hóa đơn đã tất toán.

- **HTTP Method:** `GET`
- **URL Path:** `/api/v1/payments/invoices/{invoiceId}/download`
- **Authentication:** `Bearer JWT` (Bắt buộc)
- **Authorized Roles:** `DRIVER`, `CUSTOMER`, `OPERATOR`, `ADMIN`
- **Resource Scope:**
  - `DRIVER` / `CUSTOMER`: Chỉ được phép tải hóa đơn mà tài khoản của mình là chủ sở hữu (`AccountId == CurrentActor.UserId`).
  - `OPERATOR` / `ADMIN`: Được phép tải hóa đơn trong phạm vi quản lý để hỗ trợ đối soát/khiếu nại.
- **Response Content-Type:** `application/pdf`
- **Response Headers:**
  - `Content-Disposition: attachment; filename="HD_{invoiceNumber}.pdf"`
  - `Content-Length: <byte_count>`

#### A. Tham số Path Parameter

| Tham số | Vị trí | Kiểu | Bắt buộc | Ràng buộc | Ý nghĩa | Ví dụ |
| :--- | :---: | :---: | :---: | :--- | :--- | :--- |
| `invoiceId` | `Path` | `UUID` | Có | UUID v4 hợp lệ | Định danh duy nhất của hóa đơn | `b3e4f5a6-7c8d-4e9f-0a1b-2c3d4e5f6a7b` |

#### B. Quy tắc Kiểm tra An toàn & Mã Trạng thái (Validation & HTTP Status Codes)

| HTTP Status | Mã lỗi (`errorCode`) | Điều kiện kích hoạt | Phản hồi mẫu / Hành vi | Hướng xử lý của Client |
| :---: | :--- | :--- | :--- | :--- |
| **`200 OK`** | N/A | Hóa đơn tồn tại, thuộc quyền sở hữu của caller, và trạng thái đã là `PAID`. | Trả về Binary PDF stream (`%PDF-1.4...`) | Trình duyệt kích hoạt download file hoặc lưu vào máy. |
| **`401 Unauthorized`** | `UNAUTHORIZED` | Không có Bearer token hoặc token đã hết hạn. | `{"success": false, "errorCode": "UNAUTHORIZED", "message": "Thiếu token xác thực..."}` | Đăng nhập lại để lấy token mới. |
| **`403 Forbidden`** | `FORBIDDEN_INVOICE_ACCESS` | Driver A cố tình truyền `invoiceId` thuộc sở hữu của Driver B. | `{"success": false, "errorCode": "FORBIDDEN_INVOICE_ACCESS", "message": "Bạn không có quyền truy cập hóa đơn này."}` | Không được phép truy cập tài nguyên của người khác. |
| **`404 Not Found`** | `INVOICE_NOT_FOUND` | Không tìm thấy hóa đơn với `invoiceId` chỉ định. | `{"success": false, "errorCode": "INVOICE_NOT_FOUND", "message": "Không tìm thấy hóa đơn..."}` | Kiểm tra lại mã `invoiceId`. |
| **`422 Unprocessable Entity`** | `UNSETTLED_PAYMENT_ORDER` | Hóa đơn đang ở trạng thái `PENDING` hoặc `FAILED` (chưa thanh toán). | `{"success": false, "errorCode": "UNSETTLED_PAYMENT_ORDER", "message": "Hóa đơn chưa hoàn tất thanh toán (Status: PENDING), không thể xuất file PDF chính thức."}` | Yêu cầu khách hàng thanh toán trước khi lấy file PDF. |

#### C. Nội dung Bố cục File PDF Hóa đơn (PDF Document Structure)
Bản PDF do `QuestPdfInvoiceGenerator` tạo ra đảm bảo:
1. **Tiêu đề & Thương hiệu:** Logo SmartPark, Tên bãi đỗ xe (`SiteName`), Mã hóa đơn (`InvoiceNumber`), Mã đơn hàng (`OrderCode`), Ngày phát hành (`IssuedAt` múi giờ ICT/GMT+7).
2. **Thông tin Khách hàng:** Tên tài xế, Biển số xe (`PlateNumber`), Phân loại phương tiện (`CAR`, `MOTORCYCLE`).
3. **Mốc thời gian:** Giờ xe vào (`EntryTime`), Giờ chốt cước ra cổng (`PricingCutoffAt`), Tổng số phút đỗ xe.
4. **Bảng phân tích cước (Line Items):** Chi tiết cước phân đoạn ban ngày/ban đêm, đơn giá, thời lượng tính phí, thành tiền.
5. **Tổng kết tài chính (Financial Summary):**
   - Tiền cước đỗ xe gốc (`SubtotalParkingFee`)
   - Tiền phạt vi phạm (`TotalPenaltyFee`) kèm lý do phạt nếu có
   - Khấu trừ đặt cọc trước (`DepositDeducted`)
   - Tiền hoàn lại do dư cọc (`SurplusRefund`)
   - **Tổng tiền thanh toán thực tế (`FinalTotalAmount`)**
   - Phương thức thanh toán (`VietQR payOS` / `Tiền mặt`).
6. **Mã vạch / Chữ ký số điện tử:** Xác thực tính bất biến của chứng từ điện tử.

---

### 2.2 API Lịch sử Hóa đơn & Thanh toán của Tài xế (Driver Invoice History)

Endpoint cho phép tài xế xem danh sách các hóa đơn của chính mình, hỗ trợ lọc và phân trang.

- **HTTP Method:** `GET`
- **URL Path:** `/api/v1/payments/invoices/my-history`
- **Authentication:** `Bearer JWT` (Bắt buộc)
- **Authorized Roles:** `DRIVER`, `CUSTOMER`
- **Resource Scope:** Tự động gán `AccountId = CurrentActor.UserId` từ Token (không cho phép override từ client).
- **Response Content-Type:** `application/json`

#### A. Tham số Query Parameters

| Tham số | Kiểu | Bắt buộc | Mặc định | Ý nghĩa | Ví dụ |
| :--- | :---: | :---: | :---: | :--- | :--- |
| `status` | `String` | Không | `null` | Lọc theo trạng thái hóa đơn: `PAID`, `PENDING`, `CANCELLED`, `REFUNDED` | `PAID` |
| `fromDate` | `ISO-8601` | Không | `null` | Lọc hóa đơn tạo từ mốc thời gian này | `2026-11-01T00:00:00Z` |
| `toDate` | `ISO-8601` | Không | `null` | Lọc hóa đơn tạo đến mốc thời gian này | `2026-11-10T23:59:59Z` |
| `pageNumber` | `Integer` | Không | `1` | Số trang cần lấy ($\ge 1$) | `1` |
| `pageSize` | `Integer` | Không | `10` | Số lượng bản ghi mỗi trang ($1 \le \text{pageSize} \le 100$) | `10` |

#### B. Phản hồi Mẫu (Response Sample — HTTP 200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "invoiceIdentity": {
          "invoiceId": "b3e4f5a6-7c8d-4e9f-0a1b-2c3d4e5f6a7b",
          "invoiceNumber": "INV-20261110-0001",
          "paymentOrderId": "c2d3e4f5-6a7b-8c9d-0e1f-2a3b4c5d6e7f",
          "orderCode": "ORD-20261110-001",
          "issuedAt": "2026-11-10T09:30:00Z",
          "pdfUrl": "http://localhost:5065/api/v1/payments/invoices/b3e4f5a6-7c8d-4e9f-0a1b-2c3d4e5f6a7b/download"
        },
        "customerReference": {
          "accountId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
          "customerType": "REGISTERED_DRIVER",
          "displayName": "Nguyễn Văn A"
        },
        "parkingSessionReference": {
          "sessionId": "e5f6a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b",
          "referenceType": "PARKING_SESSION",
          "siteId": "f7a8b9c0-1d2e-3f4a-5b6c-7d8e9f0a1b2c",
          "siteName": "Bãi đỗ xe Landmark 81",
          "vehicleType": "CAR",
          "plateNumber": "51A-123.45"
        },
        "timing": {
          "entryTime": "2026-11-10T07:00:00Z",
          "pricingCutoffAt": "2026-11-10T09:30:00Z",
          "totalDurationMinutes": 150,
          "billableDurationMinutes": 150
        },
        "tariffPolicy": {
          "tariffId": "8a123456-5717-4562-b3fc-2c963f66afa6",
          "tariffVersion": 1,
          "policyName": "Biểu giá tiêu chuẩn Ô tô",
          "freeParkingGraceMinutes": 15
        },
        "financialSummary": {
          "subtotalParkingFee": 60000.0,
          "totalPenaltyFee": 0.0,
          "discountAmount": 0.0,
          "finalTotalAmount": 60000.0,
          "currency": "VND",
          "depositDeducted": 0.0,
          "surplusRefund": 0.0
        },
        "financialStatus": {
          "status": "PAID",
          "isSettled": true,
          "paidAt": "2026-11-10T09:31:00Z",
          "paymentMethod": "PAYOS",
          "transactionReference": "TX-1731231060",
          "gatewayTransactionId": "PAYOS-987654321"
        },
        "lineItems": [
          {
            "itemType": "DAY_HOURLY",
            "description": "Cước đỗ xe ban ngày (07:00 - 09:30)",
            "startTime": "2026-11-10T07:00:00Z",
            "endTime": "2026-11-10T09:30:00Z",
            "durationMinutes": 150,
            "coveredMinutes": 0,
            "chargeableMinutes": 150,
            "unitRate": 25000.0,
            "unit": "HOURLY_BLOCK",
            "amount": 60000.0
          }
        ],
        "pdfUrl": "http://localhost:5065/api/v1/payments/invoices/b3e4f5a6-7c8d-4e9f-0a1b-2c3d4e5f6a7b/download"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

---

## 3. Kiến trúc Triển khai (Architectural & Clean Architecture Compliance)

Mô hình hiện thực tuân thủ tuyệt đối quy tắc **Inward Dependency Rule** và **Ports & Adapters**:

```
+-------------------------------------------------------------------------+
|                              API LAYER                                  |
|   InvoicesController (GET /{id}/download, GET /my-history)              |
+-------------------------------------------------------------------------+
                                    |
                                    v (MediatR Query)
+-------------------------------------------------------------------------+
|                          APPLICATION LAYER                              |
|   - DownloadInvoicePdfQuery / DownloadInvoicePdfQueryHandler           |
|   - GetMyPaymentInvoicesQuery / GetMyPaymentInvoicesQueryHandler       |
|   - Port: IPdfInvoiceGenerator                                          |
|   - Port: IInvoiceRepository                                            |
|   - Validator: InvoiceAuthorizationValidator                            |
+-------------------------------------------------------------------------+
             |                                              |
             | (Implements Port)                            | (Implements Port)
             v                                              v
+---------------------------------------+   +-----------------------------+
|        INFRASTRUCTURE LAYER           |   |      PERSISTENCE LAYER      |
|   - QuestPdfInvoiceGenerator          |   |   - InvoiceRepository       |
|   - QuestPDF Engine (Community)       |   |   - EF Core Paged Queries   |
+---------------------------------------+   +-----------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                            DOMAIN LAYER                                 |
|   - Invoice, PaymentOrder, PaymentTransaction (100% POCO)               |
+-------------------------------------------------------------------------+
```

---

## 4. Kế hoạch Kiểm thử & Đối soát Postman

| Mã kịch bản | Thư mục Postman | Phương thức / URL | Trạng thái mong đợi | Assertion chính |
| :---: | :--- | :--- | :---: | :--- |
| `2.15` | `02 - Customer Invoice & Gate Checkout` | `GET /api/v1/payments/invoices/my-history` | `200 OK` | Kiểm tra phong bì `items`, `totalCount`, `pdfUrl`. |
| `2.16` | `02 - Customer Invoice & Gate Checkout` | `GET /api/v1/payments/invoices/{{spark321InvoiceId}}/download` | `200 OK` | `Content-Type: application/pdf`, filename `HD_*.pdf`, binary stream `%PDF`. |
| `2.17` | `02 - Customer Invoice & Gate Checkout` | `GET /api/v1/payments/invoices/{{spark321InvoiceId}}/download` (Driver B) | `403 Forbidden` | `errorCode == "FORBIDDEN_INVOICE_ACCESS"`. |
| `2.18` | `02 - Customer Invoice & Gate Checkout` | `GET /api/v1/payments/invoices/my-history?status=PAID&pageNumber=1&pageSize=10` | `200 OK` | Lọc đúng status, metadata phân trang chính xác. |
