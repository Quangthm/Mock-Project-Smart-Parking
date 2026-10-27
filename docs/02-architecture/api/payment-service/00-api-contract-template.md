# [Tên Tính năng / Endpoint] — API Contract Specification

> **Feature Key:** `[Mã Jira, VD: SPARK-229]`  
> **Service Owner:** `[Tên Microservice cung cấp, VD: Payment Service]`  
> **Target Interface / Route:** `[C# Interface hoặc HTTP Route, VD: POST /api/v1/pricing/calculate]`  
> **Document Revision:** `v1.0` *(Phiên bản tài liệu đặc tả của file này, bắt đầu từ v1.0)*  
> **API Version:** `[v1 | N/A | TBD]` *(Chỉ áp dụng khi contract có HTTP route do SmartPark quản lý và có versioning; ghi N/A nếu không áp dụng, TBD nếu chưa quyết định. Độc lập với Document Revision.)*  
> **Contract Status:** `[Backlog | Draft | In Review | Approved | Deprecated]`  
> **Communication Type:** `[Client-to-Backend HTTP | Inter-Service S2S HTTP | Internal Domain Interface | Webhook / Asynchronous]`  
> **Author:** `[Tên tác giả]`  
> **Reviewer:** `[Tên Reviewer / Auditor - bắt buộc là người khác với Author]`  
> **SRS / BR Baseline:** `[Phiên bản tài liệu đối chiếu, VD: SRS v0.9 (2026-10-05), BR v0.8.5]`  

---

### Lịch sử thay đổi (Revision History / Change Log) `[Required]`

| Revision | Ngày | Tác giả | Người duyệt | Loại thay đổi | Mô tả thay đổi / Jira Key |
| :---: | :---: | :--- | :--- | :--- | :--- |
| `v1.0` | `YYYY-MM-DD` | `[Author]` | `[Reviewer]` | Initial | Khởi tạo tài liệu đặc tả ban đầu theo task `[Jira-Key]` |

*Quy ước phân loại thay đổi:*
- **Initial:** Soạn thảo bản đầu tiên của tài liệu contract này.
- **Clarification / Non-breaking:** Bổ sung ví dụ, làm rõ giải thuật, sửa lỗi chính tả, bổ sung trường tùy chọn không phá vỡ client cũ (tăng Document Revision: `v1.0` $\rightarrow$ `v1.1`, giữ nguyên API Version `/api/v1/`).
- **Sửa lỗi tính toán (Bug Fix):** Sửa lỗi implementation khiến kết quả sai lệch so với quy tắc đã duyệt (tăng Document Revision: `v1.0` $\rightarrow$ `v1.1`, giữ nguyên API Version `/api/v1/`).
- **Thay đổi chính sách giá (Business Policy / Data Change):** Thay đổi bảng giá, mức phí, khung giờ áp dụng theo cấu hình bãi xe $\rightarrow$ Đây là thay đổi cấu hình dữ liệu (Configuration Data), **KHÔNG** làm thay đổi API Contract hay API Version.
- **Breaking Change:** Thay đổi cấu trúc trường bắt buộc, xóa trường, đổi kiểu dữ liệu hoặc thay đổi hành vi hợp đồng làm phá vỡ tích hợp của consumer hiện tại (yêu cầu khởi tạo contract mới cho API Version mới, ví dụ `/api/v2/`, file `...-v2.contract.md` với Document Revision ban đầu là `v1.0`).

---

## 1. Tổng quan & Phạm vi nghiệp vụ (Overview & Business Scope) `[Required]`

### 1.1 Mục đích nghiệp vụ (Business Objective) `[Required]`
[Mô tả ngắn gọn, súc tích mục đích của API Contract này. Nó giải quyết bài toán gì trong hệ thống SmartPark? Phục vụ cho đối tượng người dùng nào hoặc bước nào trong quy trình kinh doanh?]

### 1.2 Ranh giới & Consumer (Consumer & Boundary Scope) `[Required]`
- **Bên tiêu thụ (Consumer / Caller):** [Ai hoặc Service nào gọi: Client FE, Operator Web, Checkout Orchestrator, hay Cổng thanh toán bên thứ ba?]
- **Service sở hữu (Owning Service):** [Microservice chịu trách nhiệm cung cấp và vận hành API này]
- **Loại giao tiếp (Communication Type):** [Chọn 1 trong 4 loại giao tiếp nêu ở metadata header]

### 1.3 Xác thực & Phân quyền (Authentication & Authorization) `[Conditional: nếu endpoint có bảo mật]`
- **Cơ chế xác thực (Auth Scheme):** [Bearer JWT / Internal Service Key / Anonymous / Webhook HMAC]
- **Quyền hạn bắt buộc (Required Roles / Permissions):** [Liệt kê các Role hoặc Permission cần thiết, ví dụ: `LOT_POLICY_MANAGE`, `SERVICE_INTERNAL`, `DRIVER`]
- **Phạm vi tài nguyên (Resource Scope):** [Kiểm tra quyền sở hữu theo Tenant ID, Site ID hoặc Account ID để chống truy cập chéo dữ liệu]

### 1.4 Chính sách Idempotency (Idempotency Policy) `[Conditional: áp dụng cho API mutating/ghi dữ liệu hoặc có tác động tài chính]`
*(Nếu endpoint là GET/Query hoặc Idempotent tự nhiên thì ghi "Không áp dụng cho endpoint đọc dữ liệu")*

- **Header quy chuẩn:** `Idempotency-Key` (chuỗi định danh duy nhất dạng UUID v4 do Client sinh ra).
- **Phạm vi duy nhất của Key (Uniqueness Scope):** Khóa tính duy nhất được quản lý theo bộ tứ:  
  `Scope = (tenant_id, consumer_id, endpoint_route, idempotency_key)` nhằm ngăn chặn xung đột key giữa các tenant, tài khoản hoặc endpoint khác nhau.
- **Hành vi xử lý chuẩn:**
  - *Cùng Key + Cùng Payload (Đã xử lý thành công):* Trả về nguyên trạng Response ban đầu đã lưu kèm header `Idempotency-Replayed: true`.
  - *Cùng Key + Khác Payload:* Từ chối ngay lập tức với duy nhất HTTP `409 Conflict`, mã lỗi `IDEMPOTENCY_PAYLOAD_MISMATCH`. Tuyệt đối không thực thi lại.
  - *Yêu cầu đang xử lý (In-flight Request):* Trả về HTTP `409 Conflict`, mã lỗi `OPERATION_IN_PROGRESS` để báo hiệu client chờ hoặc kiểm tra trạng thái.
  - *Xử lý lỗi kỹ thuật nội bộ (Internal Transient Errors - 500 DB timeout):* Client được phép retry với cùng `Idempotency-Key` và nguyên vẹn payload ban đầu (áp dụng Exponential Backoff kèm Jitter).
  - *Xử lý kết quả nghiệp vụ thất bại dứt khoát (Business Rejections):*  
    *Ví dụ: Giao dịch bị từ chối do thẻ hết hạn, tài khoản không đủ tiền, vi phạm quy tắc nghiệp vụ.*  
    Đây là kết quả xử lý nghiệp vụ cuối cùng hợp lệ và đã hoàn tất. Khi client gọi lại với cùng key, hệ thống **TRẢ LẠI NGUYÊN TRẠNG KẾT QUẢ THẤT BẠI ĐÓ** (kèm `Idempotency-Replayed: true`). Client **KHÔNG ĐƯỢC PHÉP** dùng lại key này để mong đợi tạo một giao dịch mới. Muốn khởi tạo giao dịch mới, Client **BẮT BUỘC PHẢI SINH MỘT `Idempotency-Key` MỚI**.
- **Ranh giới Cam kết Dữ liệu (Commitment Boundaries):**
  - *Nội bộ DB (Internal Database Scope):* Với các thao tác thuần nội bộ (tạo hóa đơn, đặt chỗ), bản ghi Idempotency và dữ liệu nghiệp vụ được commit nguyên tử (Atomic transaction) trong cùng một DB transaction; TTL tối thiểu `24 giờ`.
  - *Cổng thanh toán bên ngoài (External Payment Gateway Boundary):* Tuyệt đối **không giả định distributed transaction nguyên tử** giữa hệ thống và Provider. Bắt buộc áp dụng mô hình **Máy trạng thái (State Machine)** và **Eventual Consistency** (xem chi tiết tại Module B).
- **Nguyên tắc xử lý Timeout & Trạng thái Chưa xác định (Indeterminate State):**
  - **Network Timeout $\neq$ Thất bại:** Mất kết nối mạng hoặc timeout khi gọi sang Payment Provider **không đồng nghĩa với việc giao dịch thất bại** (Provider có thể đã trừ tiền nhưng response bị mất trên đường truyền).
  - **Chống trừ tiền hai lần (Double Charging Prevention):** Tuyệt đối không tự suy diễn giao dịch thất bại để retry hoặc cho phép tạo đơn mới. Giao dịch phải được giữ ở trạng thái `UNKNOWN` / `PROCESSING`, kích hoạt cơ chế chủ động tra cứu (Lookup / Query API) sang Gateway hoặc chờ Webhook/Đối soát trước khi đưa ra kết luận cuối cùng.
- *[OPEN QUESTION: Cơ chế lưu trữ idempotency store (Redis tập trung có TTL vs Bảng chuyên dụng trong PostgreSQL service DB) sẽ được đội ngũ Hạ tầng / Tech Lead chốt khi thiết kế chi tiết triển khai].*

### 1.5 Bảng truy vết yêu cầu hai chiều (Bi-directional Traceability) `[Required]`

#### Chiều 1: Từ Contract $\rightarrow$ Yêu cầu SRS / Business Rules
| Mã yêu cầu | Phân loại | Nội dung điều khoản nghiệp vụ | Mục hiện thực trong Contract |
| :--- | :---: | :--- | :--- |
| **[Mã FR/BR/US]** | [FR / BR / US] | [Tóm tắt nội dung yêu cầu liên quan] | Mục [X.X] |

#### Chiều 2: Từ Yêu cầu $\rightarrow$ Kịch bản kiểm thử (Verification & Test Coverage)
| Mã yêu cầu | Tiêu chuẩn nghiệm thu (Acceptance Criteria) | Test Scenario / Test Case ID | Kết quả mong đợi |
| :--- | :--- | :--- | :--- |
| **[Mã FR/BR]** | [Tiêu chí đánh giá hành vi thành công/thất bại] | `TC_[Feature]_[01]` | [Hành vi hoặc giá trị mong đợi] |

---

## 2. Đặc tả Hợp đồng Giao tiếp (Communication Contract Specification) `[Required]`

*(Chỉ điền các tiểu mục tương ứng với loại giao tiếp thực tế của Contract)*

### 2.1 Hợp đồng HTTP REST API `[Conditional: áp dụng cho HTTP Endpoints]`
- **HTTP Method:** `[POST / GET / PUT / DELETE / PATCH]`
- **URL Path:** `/api/v1/[service-or-domain]/[endpoint-name]`
- **Headers:**
  - `Content-Type: application/json`
  - `Authorization: Bearer <token>` *(nếu có)*
  - `Idempotency-Key: <uuid-v4>` *(nếu có)*
  - `traceparent: <w3c-trace-context>` *(nếu có)*

#### A. Tham số Path & Query Parameters `[Conditional: nếu có]`
| Tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc / Enum | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :---: | :--- | :--- |
| `[param_name]` | `Path/Query` | `[String/UUID/Int]` | `[Có/Không]` | `[min/max/regex]` | [Mô tả mục đích tham số] |

#### B. Request Body Schema `[Conditional: nếu method yêu cầu body]`
*(Lưu ý phân biệt rõ ba khái niệm: `Bắt buộc` = client phải gửi field; `Nullable` = field được phép mang giá trị null; `Mặc định` = giá trị hệ thống tự gán khi client không gửi).*

| Tên trường (JSON) | Kiểu dữ liệu | Bắt buộc | Cho phép Null | Giá trị mặc định | Ràng buộc & Định dạng | Ý nghĩa nghiệp vụ |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `[field_name]` | `[Type]` | `[Có/Không]` | `[Có/Không]` | `[null/value]` | `[Regex/Range/Enum]` | [Mô tả chi tiết và quy tắc validate] |

#### C. Response Body Schema (Success Path) `[Required]`
*(Tuân thủ cấu trúc envelope chuẩn của hệ thống: `{ "success": true, "data": { ... } }`)*

- **HTTP Status:** `200 OK` hoặc `201 Created`

| Tên trường (JSON) | Kiểu dữ liệu | Cho phép Null | Ý nghĩa nghiệp vụ & Quy tắc sinh dữ liệu |
| :--- | :--- | :---: | :--- |
| `success` | `Boolean` | Không | Luôn là `true` khi xử lý thành công |
| `data.[field_name]` | `[Type]` | `[Có/Không]` | [Ý nghĩa chi tiết của trường dữ liệu trả về] |

---

### 2.2 Quy ước Tiền tệ và Thời gian (Currency & Timestamp Conventions) `[Required khi payload có tiền hoặc thời gian]`

#### Quy ước Tiền tệ (Currency Standard):
- **Đơn vị tiền tệ chính thức:** VND.
- **Biểu diễn trên API công khai (Public REST API):**
  - **Bắt buộc là Số nguyên (`integer` / định dạng `int64` trong JSON/OpenAPI)** theo đơn vị VND. Vì trong thực tế giao dịch tại Việt Nam, tiền tệ không có đơn vị xu/hào và mọi số tiền thanh toán cuối cùng đều là số nguyên.
  - *An toàn số nguyên (Integer Safety):* Số tiền phí đỗ xe thực tế nằm trong khoảng vài chục nghìn đến vài chục triệu VND, hoàn toàn nằm trong giới hạn an toàn của JavaScript (`Number.MAX_SAFE_INTEGER` $\approx 9 \times 10^{15}$ VND - 9 triệu tỷ VND), đảm bảo không bao giờ xảy ra lỗi sai số khi Client parse JSON.
- **Tính toán trung gian nội bộ:** Trong mã nguồn engine Backend, bắt buộc sử dụng kiểu số thực chính xác cao (`decimal` trong C# / .NET, `numeric` trong PostgreSQL). Tuyệt đối không dùng `float` hay `double` trong bất kỳ phép toán tài chính nào.
- **Quy tắc làm tròn (Rounding Rule):** Tính toán chi tiết các phân đoạn bằng `decimal` không làm tròn sớm (No premature rounding); chỉ thực hiện làm tròn **1 lần duy nhất** ở kết quả tổng cuối cùng trước khi chuyển sang số nguyên VND.
  - *[OPEN QUESTION: Phương thức làm tròn cụ thể (Làm tròn số học Half-Up theo kế toán Việt Nam vs Làm tròn lên bội số nghìn Ceiling) sẽ do cấu hình chính sách bãi xe (PricingPolicy) quyết định khi chốt nghiệp vụ với Ban Quản Lý].*
- **Trường hợp duy nhất dùng Chuỗi (`string`):** Chỉ dùng chuỗi cho số tiền khi API phục vụ mục đích hiển thị định dạng sẵn (Display/i18n formatted string kèm dấu phân cách và ký hiệu tiền tệ, ví dụ `"15.000 đ"`), hoặc khi tích hợp với cổng thanh toán bên thứ ba quy định bắt buộc string. Đối với dữ liệu tính toán, không tùy tiện dùng chuỗi.

#### Quy ước Thời gian (Timestamp & Business Time):
- **Timestamp thời điểm thực tế (Point-in-Time):** Biểu diễn một thời điểm xác định trên trục thời gian toàn cầu bằng chuỗi chuẩn ISO-8601 kèm timezone offset rõ ràng (ví dụ: `2026-10-12T08:30:00+07:00` hoặc `2026-10-12T01:30:00Z`). Dùng để ghi nhận chính xác mốc xe vào/ra.
- **Giờ địa phương & Giờ nghiệp vụ (Local Business Time):**
  - Dùng để xác định khung biểu giá (Ban ngày vs Ban đêm, Cao điểm vs Thấp điểm) và ranh giới ngày kinh doanh (Midnight `00:00:00`).
  - *Chính sách Timezone đa khu vực:* Mặc định cho thị trường Việt Nam là `Asia/Ho_Chi_Minh` (UTC+7). Hệ thống không hardcode cố định mà hỗ trợ cấu hình múi giờ theo từng cơ sở bãi xe (`ParkingSite.TimeZoneId`) để sẵn sàng mở rộng đa khu vực. Khi tính giá, engine sẽ chuyển đổi timestamp vào/ra sang giờ địa phương của bãi xe đó.
- **Ngày lịch (Calendar Date):** Biểu diễn dạng `YYYY-MM-DD` khi chỉ mang ý nghĩa ngày hiệu lực chính sách hoặc thống kê báo cáo theo ngày.

---

### 2.3 Tham chiếu OpenAPI 3.1 Specification `[Conditional: áp dụng cho HTTP API khi đạt trạng thái In Review hoặc Approved]`
*(Nguyên tắc cốt lõi: Không duy trì thủ công song song hai bộ schema độc lập chi tiết nếu chúng trùng lặp thông tin).*

- **Đường dẫn file OpenAPI spec:** `[docs/02-architecture/api/.../openapi.yaml hoặc đường dẫn tương ứng]`
- **Phân định Nguồn sự thật (Single Source of Truth - SSOT):**
  - **OpenAPI 3.1 Spec (`.yaml`/`.json`):** Là **SSOT cho cấu trúc kỹ thuật** (tên trường, kiểu dữ liệu, định dạng, enum, constraints min/max, header, cấu trúc error envelope). File này được sử dụng trực tiếp để tự động sinh DTOs, cấu hình middleware xác thực và làm mock server cho Frontend/QA.
  - **Tài liệu Markdown:** Là **SSOT cho ngữ cảnh nghiệp vụ** (Business context, giải thích thuật toán, ma trận phân quyền, ma trận mã lỗi thực tế, edge cases và cơ sở thiết kế). Bảng schema trong Markdown chỉ tóm tắt các trường nghiệp vụ trọng tâm kèm giải thích ý nghĩa.
- **Trách nhiệm đồng bộ:**
  - Khi Contract đạt trạng thái `In Review` hoặc `Approved`, Author chịu trách nhiệm cập nhật đồng bộ cả Markdown và OpenAPI spec.
  - Reviewer có trách nhiệm kiểm tra đối chiếu tính nhất quán giữa hai tài liệu trước khi phê duyệt.
  - Mọi thay đổi kỹ thuật về schema phải được phản ánh trước hoặc đồng thời trên OpenAPI spec.

---

### 2.4 Hợp đồng C# Application / Domain (Internal Interface) `[Conditional: áp dụng khi contract là Internal Service Interface]`
*(Dành cho các interface nội bộ gọi trực tiếp qua DI container trong cùng một microservice, không phơi ra HTTP).*

```csharp
public interface I[FeatureName]
{
    Task<[FeatureName]Result> ExecuteAsync([FeatureName]Request request, CancellationToken cancellationToken = default);
}
```

---

## 3. Các module nghiệp vụ theo Domain (Domain-Specific Modules) `[Conditional Appendices]`

*(Phần này là phụ lục có điều kiện: chỉ chọn Module phù hợp với bản chất nghiệp vụ của endpoint. Bỏ qua nếu endpoint chỉ là CRUD/Query thông thường).*

---

### 🌟 [MODULE A]: Biến thể Tính toán Tài chính (Calculation Variant — VD: Pricing, Billing, Penalty)

#### A.1 Các bước thực thi giải thuật (Step-by-step Calculation Engine)
1. **Bước 1 (Validate input):** Kiểm tra tính hợp lệ dữ liệu (timestamps `Exit >= Entry`, định dạng loại xe, cấu hình policy).
2. **Bước 2 (Free Duration Check):** Kiểm tra thời gian đỗ có $\le$ thời gian miễn phí cấu hình không. Nếu thỏa mãn: `finalAmount = 0`, gắn cờ `isFreeStay = true`.
3. **Bước 3 (Interval Slicing & Midnight Crossing):**
   - Tách khoảng đỗ xe tại ranh giới ngày `00:00:00` theo giờ địa phương.
   - Cắt các phân đoạn theo khung giờ hiệu lực của biểu giá (`time-block pricing`).
4. **Bước 4 (Segment Charges Calculation):** Tính tiền từng phân đoạn bằng phép toán số thực chính xác cao (`decimal`), **tuyệt đối không làm tròn tiền tệ sớm** (No Premature Rounding).
5. **Bước 5 (Aggregation & Single Rounding):** Cộng dồn các phân đoạn và áp dụng quy tắc làm tròn tiền tệ **1 lần duy nhất** ở tổng cuối cùng.
6. **Bước 6 (Snapshot Packaging):** Đóng gói metadata thành JSON Snapshot để lưu trữ bất biến phục vụ kế toán/hóa đơn.

---

### 🌐 [MODULE B]: Biến thể Tích hợp Cổng thanh toán & Webhook (Integration Variant — VD: VNPay, MoMo, ZaloPay)

#### B.1 Xác thực chữ ký số Callback (Webhook HMAC Verification)
- **Tài liệu tham chiếu Provider:** [Tên cổng thanh toán và link tài liệu tích hợp chính thức của đối tác]
- **Thuật toán & Quy tắc:** Ghi rõ thuật toán (`HMAC-SHA256` hoặc `HMAC-SHA512`), cách sắp xếp các trường theo thứ tự từ điển, khóa bí mật ký (`SecretKey`), và cách so sánh chữ ký an toàn (constant-time comparison). Sai chữ ký $\rightarrow$ từ chối ngay lập tức.

#### B.2 Máy trạng thái giao dịch & Xử lý Trạng thái Chưa xác định (Payment State Machine & Indeterminate Handling)
- **Các trạng thái tham khảo:** `PENDING`, `PROCESSING`, `UNKNOWN`, `SUCCESS`, `FAILED`, `REFUND_PENDING`, `REFUNDED`. Contract cụ thể phải chọn và định nghĩa các trạng thái thực sự cần dùng; ánh xạ trạng thái từ Provider sang trạng thái nội bộ là `[OPEN QUESTION]` cho đến khi đối chiếu tài liệu Provider.
- **Quy tắc chuyển trạng thái tối thiểu:**
  - `PENDING` / `PROCESSING` / `UNKNOWN` có thể chuyển sang `SUCCESS` hoặc `FAILED` chỉ khi có bằng chứng đủ tin cậy từ Provider hoặc quy trình đối soát.
  - Không cho phép callback đến trễ làm giao dịch đã `SUCCESS` lùi về `FAILED`; sự kiện mâu thuẫn phải được ghi log và đưa vào xử lý/đối soát.
  - `REFUNDED` chỉ áp dụng sau khi khoản thanh toán đã `SUCCESS` và Provider xác nhận hoàn tiền; nếu cần theo dõi hoàn tiền bất đồng bộ, dùng trạng thái trung gian như `REFUND_PENDING`.
  - Không coi đây là một state machine phổ quát cho mọi Provider; mỗi contract phải ghi rõ các transition hợp lệ, transition bị từ chối và cách xử lý sự kiện lặp/sai thứ tự.
- **Ràng buộc xử lý Timeout & Network Error:**
  - Khi gọi Provider bị Timeout (504 Gateway Timeout) hoặc mất kết nối mạng $\rightarrow$ chuyển trạng thái giao dịch thành **`UNKNOWN`** (hoặc giữ `PENDING` kèm cờ `lookup_required: true`).
  - **Quy tắc ngăn chặn trừ tiền hai lần (Double-Charge Prevention):** Hệ thống tuyệt đối không được tự ý chuyển thành `FAILED` và cấm Client tạo giao dịch mới cho đến khi xác minh xong trạng thái giao dịch cũ.
  - **Cơ chế xác thực kết quả bắt buộc:**
    1. Kích hoạt Job/Task tra cứu tự động (**Transaction Query / Lookup Order API**) sang Provider bằng mã đơn hàng duy nhất (`client_transaction_id`).
    2. Nếu Provider xác nhận giao dịch đã thành công $\rightarrow$ cập nhật sang `SUCCESS`, thông báo cho người dùng, hoàn tất thanh toán.
    3. Nếu Provider xác nhận giao dịch thất bại hoặc chưa từng phát sinh giao dịch $\rightarrow$ cập nhật sang `FAILED`, mở khóa để người dùng chọn phương thức thanh toán khác hoặc thử lại.
    4. Nếu các kênh real-time đều không phản hồi $\rightarrow$ chuyển sang hàng đợi **Đối soát định kỳ (Reconciliation)** cuối ngày.

#### B.3 Xử lý Callback trễ, trùng lặp và sai thứ tự (Out-of-order Callbacks)
- Dùng `idempotency_key` và mã giao dịch đối tác (`gateway_transaction_id`) để chặn xử lý trùng lặp (Idempotent Webhook Consumer).
- **Khóa trạng thái (Optimistic Locking):** Nếu trạng thái đơn hàng trong database đã là `SUCCESS` mà nhận được callback `FAILED` đến sau do độ trễ mạng $\rightarrow$ bỏ qua và ghi log audit cảnh báo; không bao giờ cho phép lùi trạng thái từ `SUCCESS` về `FAILED`.

---

### 3.3 Ma trận các ca biên quan trọng (Critical Edge Cases Matrix) `[Conditional]`
*(Bắt buộc khi endpoint có logic tính toán phức tạp, phân chia khoảng thời gian, máy trạng thái giao dịch thanh toán, xử lý đồng thời hoặc rủi ro tài chính/nghiệp vụ cao. Không bắt buộc đối với các API CRUD hoặc Query tra cứu đơn giản — các API đơn giản chỉ cần kịch bản kiểm thử cơ bản tại Mục 1.5 và ví dụ tại Mục 6, tránh lập bảng ca biên mang tính hình thức).*

| Kịch bản biên | Đầu vào mô phỏng | Hành vi kỳ vọng của hệ thống | Căn cứ nghiệp vụ / Mã BR |
| :--- | :--- | :--- | :--- |
| **[Tên kịch bản]** | [Mô tả input đặc biệt] | [Hệ thống tính toán hoặc xử lý như thế nào?] | [Mã BR/FR liên quan] |

---

## 4. Danh mục Mã lỗi & Phản hồi (Error & Exception Handling) `[Required]`

### 4.1 Cấu trúc phản hồi lỗi chuẩn (Standard Error Envelope)
Mọi phản hồi lỗi của hệ thống đều tuân thủ cấu trúc thống nhất:

```json
{
  "success": false,
  "errorCode": "INVALID_INPUT",
  "message": "Dữ liệu yêu cầu không hợp lệ.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": [
    {
      "field": "exitTime",
      "message": "Thời gian ra bãi không thể trước thời gian vào bãi."
    }
  ],
  "timestamp": "2026-10-12T08:00:00Z"
}
```

*Quy tắc bảo mật & tích hợp:*
- Consumer lập trình phụ thuộc vào mã máy `errorCode`, không parse chuỗi `message`.
- Tuyệt đối không để lộ stack trace, thông tin server nội bộ hoặc credentials trong `message` và `errors`.
- `traceId` bắt buộc có để đối soát log xuyên suốt các microservice.

### 4.2 Bảng mã lỗi thực tế của Endpoint (Endpoint Error Matrix)
*(Chỉ liệt kê các mã lỗi mà endpoint này thực tế có thể phát sinh, không copy máy móc)*

| HTTP Status | Mã lỗi nội bộ (`errorCode`) | Điều kiện kích hoạt | Thông điệp mẫu (`message`) |
| :---: | :--- | :--- | :--- |
| **400** | `INVALID_INPUT` / `MALFORMED_REQUEST` | Cú pháp sai, thiếu header bắt buộc, body không parse được JSON | "Dữ liệu yêu cầu không đúng định dạng." |
| **401** | `UNAUTHORIZED` | Thiếu token, token hết hạn hoặc sai chữ ký HMAC | "Yêu cầu xác thực danh tính không thành công." |
| **403** | `FORBIDDEN` | Token hợp lệ nhưng không đủ quyền thao tác tài nguyên | "Không có quyền thực hiện thao tác này." |
| **404** | `RESOURCE_NOT_FOUND` | Không tìm thấy bãi xe hoặc chính sách giá | "Không tìm thấy tài nguyên yêu cầu." |
| **409** | `CONFLICT` / `IDEMPOTENCY_PAYLOAD_MISMATCH` | Xung đột trạng thái hoặc cùng Idempotency-Key nhưng khác payload | "Xung đột dữ liệu hoặc Idempotency key không khớp payload." |
| **409** | `OPERATION_IN_PROGRESS` | Yêu cầu cùng Idempotency-Key đang được xử lý | "Yêu cầu đang được xử lý. Vui lòng kiểm tra trạng thái trước khi thử lại." |
| **422** | `UNPROCESSABLE_ENTITY` | Cú pháp JSON đúng nhưng vi phạm logic nghiệp vụ (VD: `Exit < Entry`) | "Dữ liệu vi phạm quy tắc nghiệp vụ." |
| **429** | `TOO_MANY_REQUESTS` | Vượt quá giới hạn tần suất gọi API | "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau." |
| **500** | `INTERNAL_SERVER_ERROR` | Lỗi máy chủ không mong muốn | "Hệ thống gặp sự cố nội bộ. Vui lòng thử lại sau." |

---

## 5. Yêu cầu phi chức năng (Non-Functional Requirements — NFRs) `[Conditional / Optional]`

*(Ghi nhận mục tiêu cụ thể của endpoint. Các giá trị dưới đây là baseline đề xuất, cần điều chỉnh theo đặc thù từng endpoint).*

- **Mục tiêu hiệu năng (Latency & Timeout Target):**
  - *Mục tiêu độ trễ:* [VD: Xử lý tính toán nội bộ $\le 50\text{ ms}$ (p95)]
  - *Timeout tối đa:* [VD: Gọi dịch vụ nội bộ timeout $3.000\text{ ms}$; Gọi cổng thanh toán bên thứ ba timeout $5.000\text{ ms}$]
- **Giới hạn tần suất (Rate Limiting Target):** [VD: Tối đa 60 requests/phút trên mỗi Client IP hoặc Account ID, nếu áp dụng]
- **Bảo mật & Che giấu dữ liệu (Data Masking):**
  - Che giấu toàn bộ số tài khoản ngân hàng, thông tin cá nhân trong mọi log trace.
  - Tuyệt đối không log thông tin thẻ (PAN, CVV) hay API Key bí mật.
- **Phân trang (Pagination) `[Conditional: áp dụng nếu API trả về danh sách]`:**
  - *Chiến lược:* Chọn `Offset-based` (`page`, `pageSize`) cho quản trị; hoặc `Cursor-based` (`cursor`, `limit`) cho dữ liệu lớn/real-time.
  - Không bắt buộc tính `totalCount` nếu tốn kém chi phí database và consumer không cần hiển thị tổng trang.

---

## 6. Các ví dụ minh họa thực tế (Worked Examples) `[Required]`

### Ví dụ 1: Trường hợp tiêu chuẩn (Happy Path)
- **Kịch bản:** [Mô tả bối cảnh và điều kiện đầu vào]
- **Request:**
```http
POST /api/v1/[endpoint] HTTP/1.1
Content-Type: application/json

{
  "exampleKey": "value"
}
```
- **Response (HTTP 200 OK):**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "success": true,
  "data": {
    "exampleResult": 123
  }
}
```

### Ví dụ 2: Trường hợp lỗi nghiệp vụ / Validation (Error Path)
- **Kịch bản:** [Mô tả điều kiện vi phạm]
- **Response (HTTP 422 Unprocessable Entity):**
```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/json

{
  "success": false,
  "errorCode": "INVALID_TIME_RANGE",
  "message": "Thời gian ra bãi không thể trước thời gian vào bãi.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": [
    {
      "field": "exitTime",
      "message": "Thời gian ra (exitTime) phải sau thời gian vào (entryTime)."
    }
  ],
  "timestamp": "2026-10-12T08:00:00Z"
}
```

### Ví dụ 3: Ca biên trọng yếu (Critical Edge Case) `[Conditional: nếu nghiệp vụ có ca biên phức tạp]`
- **Kịch bản:** [Mô tả ca biên, ví dụ: Đỗ xe qua nửa đêm, hoặc đỗ xe dưới ngưỡng miễn phí]
- **Phép tính & Response tương ứng:** [Trình bày rõ các bước giải thuật tính ra kết quả]

---

## 7. Ghi chú Triển khai & Ánh xạ Dữ liệu (Implementation Notes & Mapping) `[Optional]`

*(Dành riêng cho Backend Developer và Data Engineer để hỗ trợ triển khai, không bắt buộc Consumer phải phụ thuộc vào).*

| Dữ liệu sinh ra từ API | Bảng đích trong DB | Cột đích / Định dạng | Mục đích sử dụng |
| :--- | :--- | :--- | :--- |
| `[Output Field]` | `[table_name]` | `[column_name]` (`[data_type]`) | [Lưu lịch sử, đóng băng snapshot hóa đơn, hay đối soát?] |

- **Phụ thuộc liên dịch vụ (Cross-service Dependencies):** [Liệt kê các API hoặc Message Broker events cần gọi sang service khác, nếu có].
