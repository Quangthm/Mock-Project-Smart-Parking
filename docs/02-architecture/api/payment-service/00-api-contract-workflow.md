# Quy trình chuẩn hóa: Quản lý vòng đời API Contract (API Contract Lifecycle Workflow)

**Mục đích**: Tài liệu này định nghĩa quy trình thao tác chuẩn (SOP — Standard Operating Procedure) cho việc thiết kế, tài liệu hóa, kiểm toán, phê duyệt và duy trì các **API Contract** trong dự án SmartPark. Quy trình này được thiết kế như một bộ tiêu chuẩn kỹ thuật (Engineering Standards) thực tế, áp dụng trước hết cho **Payment Service** và sẵn sàng mở rộng đồng bộ cho toàn bộ các microservices khác (`User`, `Parking`, `Reservation`...). Mọi thành viên trong team và AI Agent đều **BẮT BUỘC** phải tuân thủ quy trình này khi tham gia phát triển API.

---

## 1. Các tài nguyên đầu vào & Phân cấp thẩm quyền (Inputs & Authority Hierarchy)

Khi thiết kế API Contract, thông tin phải được thu thập và đối chiếu theo thứ bậc thẩm quyền sau:

1. **Tài liệu yêu cầu đã được phê duyệt (Confirmed Requirements Baseline)**:
   - *SRS hiện hành:* `docs/01-requirements/srs/smartpark-srs-v0.9.md`
   - *Business Rules hiện hành:* `docs/01-requirements/business-rules/smartpark-business-rules-v0.8.5.md`
2. **Ranh giới dịch vụ & Mô hình dữ liệu (Service Boundaries & Persistence Baseline)**:
   - Schema DDL tương ứng: `scripts/database/microservices/[tên-service]-db.sql`
   - Đặc tả ranh giới: `docs/02-architecture/service-boundaries/`
3. **Biểu mẫu tiêu chuẩn (Standard Template)**:
   - `docs/02-architecture/api/[tên-service]/00-api-contract-template.md`

> **Nguyên tắc phân định nguồn sự thật:**
> - Phân biệt rõ: (1) Quy định đã được dự án xác nhận, (2) Đề xuất chuẩn hóa kỹ thuật, và (3) Quyết định nghiệp vụ/kiến trúc còn mở (`OPEN` / `UNRESOLVED`).
> - Tuyệt đối không tự biến các giả định hoặc đề xuất cá nhân thành quy định nghiệp vụ đã được phê duyệt. Mọi điểm chưa rõ phải được gắn nhãn `[OPEN QUESTION]` và chuyển lên người có thẩm quyền quyết định.

---

## 2. Nguyên tắc kỹ thuật cốt lõi (Core Engineering Principles)

### 2.1 Phân biệt API Contract và Implementation Details
API Contract tập trung vào **cam kết giao tiếp quan sát được từ phía Consumer** (bên sử dụng API), bao gồm:
- Mục đích, phạm vi, Actor/Consumer, phương thức xác thực và phân quyền.
- Giao thức, HTTP method, URL path, headers, request schema, response schema và HTTP status codes.
- Quy tắc validation, mã lỗi chuẩn (`errorCode`), cấu trúc chi tiết lỗi và hành vi khi lỗi.
- Quy tắc nghiệp vụ chi phối kết quả đầu ra, chính sách Idempotency, timeout, retry và tính nhất quán.

**Phân biệt rõ 4 loại giao tiếp thực tế:**
1. **Client-to-Backend HTTP API:** Giao tiếp giữa Frontend (Web/Mobile) và Backend qua API Gateway.
2. **Inter-Service S2S HTTP API:** Giao tiếp trực tiếp giữa các microservice nội bộ.
3. **Internal Application / Domain Interface:** Hợp đồng Interface C# nội bộ trong cùng một service (dành cho kiến trúc Clean Architecture/Modular).
4. **Webhook / Asynchronous Communication:** Giao tiếp đẩy sự kiện hoặc callback từ bên thứ ba (VNPay, MoMo, ZaloPay...) hoặc qua Message Broker.

*Quy tắc áp dụng:* Chỉ yêu cầu các nội dung tương ứng với đúng loại giao tiếp thực tế. Không bắt buộc mọi contract HTTP phải chứa C# interface, thuật toán nội bộ hay cấu trúc bảng database.

### 2.2 Phân loại mức độ bắt buộc (Requirement Levels)
Mọi nội dung trong template và quy trình được phân loại minh bạch thành 3 cấp độ:
- **[Required] - Bắt buộc:** Áp dụng cho mọi API Contract thuộc phạm vi loại giao tiếp đó (ví dụ: HTTP method, path, request/response schema, error envelope schema, Happy path và Error path ví dụ).
- **[Conditional] - Có điều kiện:** Bắt buộc khi endpoint hoặc nghiệp vụ đáp ứng điều kiện tương ứng:
  - `Idempotency-Key`: Bắt buộc với API ghi dữ liệu, tạo giao dịch hoặc có tác động dòng tiền/tài chính.
  - `HMAC SHA256` & Webhook State Machine: Bắt buộc với Webhook cổng thanh toán bên thứ ba.
  - `Critical Edge Cases Matrix`: Bắt buộc khi có logic tính toán phức tạp, phân chia thời gian, xử lý đồng thời hoặc rủi ro tài chính cao (không ép buộc lập bảng hình thức đối với API CRUD/Query đơn giản).
  - `C# Application Interface`: Bắt buộc đối với Internal Domain Contract.
  - `OpenAPI Spec Link`: Bắt buộc khi contract tiến tới trạng thái `In Review` và `Approved`.
- **[Optional] - Tùy chọn:** Bổ sung khi mang lại giá trị thực tế (ví dụ: Database column mapping, mở rộng NFRs chi tiết, ví dụ cho các ca ít gặp).

### 2.3 Chống Over-Engineering & Thích ứng Thực tế (Pragmatic Engineering)
Tài liệu sinh ra để phục vụ công việc và phối hợp giữa các thành viên, không tạo thủ tục hành chính hình thức:
- Không bắt mọi endpoint phải khai báo đủ tất cả các HTTP status code nếu endpoint đó không bao giờ phát sinh mã lỗi đó.
- Không bắt buộc mọi contract CRUD/Query đơn giản phải tạo bảng Edge Cases nếu kịch bản kiểm thử tại mục 1.5 và ví dụ tại mục 6 đã đủ rõ.
- Không áp đặt cùng một SLA độ trễ hoặc cùng một rate limit lên tất cả API.
- Không ép buộc mọi API danh sách phải tính toán `totalCount` nếu dữ liệu lớn và consumer chỉ cần Cursor pagination.
- **Vai trò Baseline:** Bộ tài liệu này là **Baseline chuẩn mực kỹ thuật** để khởi động và thống nhất cách viết contract cho Payment Service (và làm tài liệu tham khảo cho các service khác). Chuẩn hóa là để phát hiện sớm các điểm chưa biết và đặt câu hỏi `[OPEN QUESTION]` lên người có thẩm quyền, không cố gắng tự quyết định trước mọi chi tiết kỹ thuật khi chưa đủ thông tin.

### 2.4 Quản lý Phiên bản Độc lập (Independent Versioning Dimensions)
Phân biệt rạch ròi 2 trục versioning hoàn toàn độc lập và không ràng buộc con số phải trùng nhau:
1. **API Version (Hợp đồng kỹ thuật trên URL/Route):** Đại diện cho phiên bản giao tiếp mà Consumer (FE/Clients) gọi tới (ví dụ: `/api/v1/pricing/calculate` vs `/api/v2/pricing/calculate`). Chỉ tăng khi có Breaking Change phá vỡ cam kết giao tiếp với consumer.
2. **Document Revision (Vòng đời tài liệu đặc tả):** Đánh số revision của chính file markdown đó (bắt đầu từ `v1.0` khi khởi tạo, tăng `v1.1`, `v1.2`... khi làm rõ, bổ sung ví dụ, sửa lỗi mô tả, hoặc sửa bug implementation).
   - *Nguyên tắc độc lập:* Khi một API Version mới ra đời (ví dụ: `/api/v2/`), contract của API v2 này sẽ được tạo thành file mới (ví dụ `...-v2.contract.md`) và phiên bản tài liệu ban đầu của nó hoàn toàn có thể là **Document Revision: `v1.0`** (bản tài liệu đầu tiên của API v2). Không ép buộc con số revision phải nhảy theo số version API.
3. **Contract Status (Trạng thái vòng đời):** Tiến trình phê duyệt tài liệu (`Backlog` $\rightarrow$ `Draft` $\rightarrow$ `In Review` $\rightarrow$ `Approved` $\rightarrow$ `Deprecated`).

### 2.5 Nguyên tắc An toàn Tài chính & Xử lý Giao dịch Phân tán (Financial & Transaction Rules)
- **Quy tắc biểu diễn tiền VND:**
  - *Trên API công khai (REST JSON):* Bắt buộc là **Số nguyên (`integer` / `int64`)** theo đơn vị VND. Thực tế thanh toán tại Việt Nam không có tiền lẻ phần thập phân (xu/hào).
  - *Độ an toàn số nguyên:* Phí đỗ xe thực tế nằm trọn vẹn trong giới hạn an toàn của JavaScript (`Number.MAX_SAFE_INTEGER` $\approx 9 \times 10^{15}$ VND - 9 triệu tỷ VND), loại trừ hoàn toàn nguy cơ sai số. Chỉ dùng `string` nếu API phục vụ hiển thị i18n format (`"15.000 đ"`).
  - *Tính toán trung gian Backend:* Bắt buộc dùng số thực chính xác cao (`decimal` trong C#, `numeric` trong SQL). Tuyệt đối không dùng `float` hay `double`.
  - *Làm tròn 1 lần ở cuối (Single Rounding):* Tính toán các chặng bằng `decimal` không làm tròn sớm; chỉ làm tròn 1 lần duy nhất ở kết quả tổng cuối cùng trước khi chuyển sang integer VND.
- **Quy tắc thời gian (Timestamp & Business Time):**
  - Timestamp biểu diễn thời điểm xác định toàn cầu (ISO-8601 UTC có offset).
  - Giờ địa phương dùng để tính toán khung giá ngày/đêm và ranh giới ngày kinh doanh (Midnight `00:00:00`). Mặc định là `Asia/Ho_Chi_Minh` (UTC+7) cho thị trường Việt Nam, và hỗ trợ cấu hình theo bãi xe (`TimeZoneId`) để mở rộng đa khu vực.
- **Chính sách Idempotency & Ranh giới Giao dịch Phân tán:**
  - Header: `Idempotency-Key` (UUID v4). Phạm vi duy nhất: `Scope = (tenant_id, consumer_id, endpoint_route, idempotency_key)`.
  - Cùng key + khác payload $\rightarrow$ Trả về duy nhất HTTP `409 Conflict` (`IDEMPOTENCY_PAYLOAD_MISMATCH`).
  - Request đang chạy $\rightarrow$ HTTP `409 Conflict` (`OPERATION_IN_PROGRESS`).
  - *Phân biệt phạm vi lưu trữ nội bộ vs Cổng thanh toán bên ngoài:*
    - **Nội bộ DB:** Đối với thao tác thuần nội bộ (tạo hóa đơn nháp, cập nhật xe), bản ghi Idempotency và dữ liệu nghiệp vụ được commit nguyên tử (Atomic transaction) trong cùng một DB transaction; TTL tối thiểu 24h.
    - **Cổng thanh toán bên ngoài (External Gateway Boundary):** Tuyệt đối **không giả định distributed transaction nguyên tử** với Provider (không thể dùng DB transaction bao bọc HTTP call ra ngoài). Bắt buộc thiết kế theo mô hình **Máy trạng thái giao dịch (State Machine)** và **Eventual Consistency**:
      1. Lưu bản ghi thanh toán nội bộ ở trạng thái `PENDING` kèm `client_transaction_id` trước khi gọi Gateway.
      2. Tiếp nhận kết quả qua Webhook/IPN bất đồng bộ hoặc kết quả trả về tức thời.
      3. Chống callback trùng lặp và xử lý callback sai thứ tự (out-of-order) bằng khóa trạng thái.
- **Xử lý Timeout & Nguyên tắc Trạng thái Chưa xác định (Indeterminate State):**
  - **Timeout $\neq$ Thất bại:** Network timeout hoặc lỗi 5xx từ Payment Gateway **không đồng nghĩa với việc giao dịch thất bại**. Provider có thể đã trừ tiền khách hàng thành công nhưng response bị mất trên đường truyền.
  - **Cấm retry hoặc tạo đơn mới mù quáng:** Khi gặp timeout hoặc mất kết nối với Provider, hệ thống **tuyệt đối không được coi là thất bại** và không cho phép client tạo giao dịch mới ngay lập tức (tránh nguy cơ trừ tiền 2 lần - Double Charging).
  - **Quy trình xử lý bắt buộc:**
    1. Giữ nguyên giao dịch ở trạng thái trung gian `UNKNOWN` hoặc `PROCESSING`.
    2. Kích hoạt cơ chế **Chủ động tra cứu (Transaction Lookup / Query API)** sang Provider bằng mã tham chiếu duy nhất hoặc chờ Webhook.
    3. Chỉ sau khi Provider xác nhận dứt khoát là giao dịch thất bại/chưa phát sinh thì mới chuyển sang `FAILED` và cho phép thanh toán lại.
    4. Nếu các kênh online không phản hồi, giao dịch được giải quyết qua quy trình **Đối soát định kỳ (Reconciliation)** cuối ngày.
- **Bảo mật PCI-DSS:** Không lưu trữ, truyền tải thông tin thẻ thô. Mọi log hệ thống phải che giấu thông tin nhạy cảm (Data Masking).

---

## 3. Quy trình chuẩn 6 bước (Standard 6-Step Workflow)

```text
[Bước 1] Thu thập ngữ cảnh & Kiểm tra ranh giới (Context & Boundary Check)
         │
         ▼
[Bước 2] Soạn thảo Contract theo Template chuẩn (Drafting)
         │
         ▼
[Bước 3] Lưu trữ và Cập nhật Index (Persisting & Indexing)
         │
         ▼
[Bước 4] Kiểm toán độc lập (Independent Peer Review)
         │
         ▼
[Bước 5] Tinh chỉnh & Phê duyệt (Refining & Approval)
         │
         ▼
[Bước 6] Chuyển giao sang Triển khai (Implementation Handoff: OpenAPI + Code + Test)
```

### Bước 1: Thu thập ngữ cảnh và Kiểm tra Ranh giới (Context & Boundary Check)
- **Đầu vào:** Đọc task Jira (`SPARK-xxx`), acceptance criteria và các tài liệu SRS/Business Rules liên quan.
- **Trách nhiệm của Tác giả (Author):**
  - Xác định rõ Consumer (FE, Service khác, hay Bên thứ ba).
  - Xác định Service sở hữu API và ranh giới quyền sở hữu dữ liệu.
  - Phân loại loại giao tiếp (HTTP S2S, HTTP Client, Internal Interface, Webhook).
  - Ghi nhận rõ các câu hỏi hoặc điểm mâu thuẫn chưa được quyết định (`[OPEN QUESTION]`). Nếu chưa rõ, đề xuất phương án và chờ xác nhận; không tự biến giả định cá nhân thành quy định đã phê duyệt.

### Bước 2: Soạn thảo Contract theo Template chuẩn (Drafting)
- **Trách nhiệm của Tác giả:**
  - Điền đầy đủ 100% các mục **[Required]** trong `00-api-contract-template.md`.
  - Áp dụng các mục **[Conditional]** tương ứng với bản chất của API (ví dụ: Module Calculation cho Pricing; Module Webhook cho Gateway; Idempotency cho Mutating API).
  - Bỏ qua các mục **[Optional]** không mang lại giá trị thực tế để tránh lãng phí thời gian.
  - Cung cấp ít nhất: 1 Happy Path và 1 Error Path. Bổ sung các Edge Cases trọng yếu khi nghiệp vụ có ca biên đáng kể; áp dụng `Critical Edge Cases Matrix` theo điều kiện tại mục 2.2, không yêu cầu bảng này cho CRUD/Query đơn giản nếu test scenario cơ bản đã đủ rõ.
  - Trạng thái tài liệu ở bước này là **`Draft`**.

### Bước 3: Lưu trữ và Cập nhật Index (Persisting & Indexing)
- **Quy ước đặt tên file:** `[số_thứ_tự]-[tên-tính-năng].contract.md` (ví dụ: `01-pricing-calculation.contract.md`).
- **Thư mục lưu trữ:** `docs/02-architecture/api/[tên-service]/`.
- **Cập nhật danh mục:** Thêm một dòng mới vào bảng danh mục trong file `README.md` của service đó với trạng thái `Draft` và revision ban đầu `v1.0`.

### Bước 4: Kiểm toán độc lập (Independent Peer Review)
- **Quy tắc độc lập bắt buộc:** Người review/kiểm toán **bắt buộc phải là người khác** so với tác giả (Peer Developer, Tech Lead, QA Engineer, hoặc Subagent Auditor độc lập). Tác giả không tự chấm bài của chính mình.
- **Trọng tâm kiểm toán:**
  - *Tính rõ ràng:* Request/Response schema có nhất quán, đủ kiểu dữ liệu, bắt buộc/nullable không?
  - *Ranh giới dịch vụ & Dữ liệu:* API có can thiệp trái phép vào domain của service khác không?
  - *Validation & Mã lỗi:* Bảng lỗi có đầy đủ các mã lỗi có ý nghĩa với consumer không? Cấu trúc lỗi có chứa `traceId` và chi tiết `errors` không?
  - *Xử lý rủi ro:* Idempotency (nếu có), timeout, retry, tính nhất quán đã được định nghĩa an toàn chưa?
  - *Khả năng kiểm thử (Testability):* Acceptance criteria và ma trận ca biên có đủ rõ để QA viết test cases không?
- **Phân loại phản hồi:** Reviewer phải tách bạch giữa **Lỗi chặn (Blocking Issues)** cần sửa ngay và **Gợi ý cải tiến (Suggestions)** có thể cân nhắc sau.
- Chuyển trạng thái sang **`In Review`**.

### Bước 5: Tinh chỉnh & Phê duyệt (Refining & Approval)
- Tác giả tiếp thu phản hồi, chỉnh sửa các điểm blocking.
- **Tiêu chuẩn phê duyệt (Approval Criteria):** Tài liệu CHỈ được chuyển sang trạng thái **`Approved`** khi:
  1. Tất cả các mục `[Required]` và `[Conditional]` thích hợp đã được điền đầy đủ, chính xác.
  2. Mọi điểm blocking được reviewer xác nhận đã giải quyết xong.
  3. Không còn mâu thuẫn quan trọng giữa tài liệu nghiệp vụ Markdown và schema kỹ thuật.
  4. Bảng Revision History đã được ghi nhận đầy đủ (Revision, Ngày, Tác giả, Reviewer, Nội dung thay đổi).
- *Lưu ý:* Phê duyệt dựa trên tiêu chí rõ ràng và mức độ rủi ro chấp nhận được, không chạy theo sự hoàn hảo hình thức tuyệt đối.

### Bước 6: Chuyển giao sang Triển khai (Implementation Handoff)
Khi Contract đã đạt trạng thái `Approved`, nó trở thành **Cam kết hợp đồng chính thức (Authoritative Contract)**:
- **Phân định Nguồn sự thật (Single Source of Truth - SSOT):**
  - **OpenAPI 3.1 Spec (`.yaml`/`.json`):** Là nguồn sự thật duy nhất cho cấu trúc kỹ thuật (schema, data types, validation constraints, error models). Được dùng để tự động sinh DTOs, cấu hình middleware xác thực request/response và dựng Mock Server cho Frontend/QA.
  - **Tài liệu Markdown:** Là nguồn sự thật cho ngữ cảnh nghiệp vụ, quy tắc giải thuật, ma trận mã lỗi thực tế, ca biên và cơ sở thiết kế.
  - *Quy tắc chống trùng lặp:* Không duy trì thủ công song song hai bộ schema chi tiết trùng lặp. Author chịu trách nhiệm cập nhật đồng bộ cả hai; Reviewer kiểm tra đối chiếu trước khi duyệt.
- **Frontend / Client Consumer:** Có thể song song phát triển giao diện dựa trên mock schema mà không cần đợi Backend hoàn thành.
- **Backend Developer:** Triển khai code bám sát 100% tên trường, kiểu dữ liệu, mã lỗi và ranh giới đã duyệt.
- **QA / Tester:** Thiết kế test suite và tự động hóa kiểm thử dựa trên Bảng truy vết hai chiều và Ma trận ca biên trong Contract.
- **Quy tắc cấm âm thầm sửa đổi:** Trong quá trình code, nếu Backend hoặc Frontend phát hiện contract cần điều chỉnh, **bắt buộc phải quay lại quy trình review để bump revision tài liệu**. Tuyệt đối không tự ý sửa code lệch khỏi contract đã duyệt.

---

## 4. Chính sách Quản lý Thay đổi & Versioning (Change Management Policy)

Khi có yêu cầu thay đổi từ SRS, đối tác, hoặc nhu cầu kỹ thuật:

1. **Nguyên tắc bất biến theo phiên bản đã duyệt (Approved Version Immutability):**
   - Không được phép âm thầm sửa đè lên một contract đã ở trạng thái `Approved` mà không để lại dấu vết trong Revision History.
2. **Đánh giá tác động phân cấp (Tiered Impact Analysis):**
   - **Cấp độ 1: Làm rõ hoặc Mở rộng tương thích ngược (Clarification / Non-breaking):**
     *Hành vi:* Thêm trường tùy chọn vào response, thêm mã lỗi chi tiết, bổ sung ví dụ minh họa hoặc làm rõ giải thuật.
     *Xử lý:* Tăng **Document Revision** (VD: `v1.0` $\rightarrow$ `v1.1`), giữ nguyên API route `/api/v1/`.
   - **Cấp độ 2: Sửa lỗi thuật toán (Bug Fix):**
     *Hành vi:* Sửa lỗi code implementation khiến kết quả trả về sai lệch so với quy tắc nghiệp vụ đã duyệt trong SRS/Contract.
     *Xử lý:* Sửa code kèm test case, cập nhật ví dụ/giải thích trong Contract nếu cần và tăng **Document Revision** (`v1.0` $\rightarrow$ `v1.1`), giữ nguyên API route `/api/v1/`.
   - **Cấp độ 3: Thay đổi chính sách / Dữ liệu cấu hình (Business Policy / Data Change):**
     *Hành vi:* Bãi xe điều chỉnh bảng giá (ví dụ: tăng từ 5.000 lên 6.000 đ/block), thay đổi khung giờ ngày/đêm theo mùa.
     *Xử lý:* Đây là thay đổi dữ liệu cấu hình trong database (`pricing_policies`), **KHÔNG PHẢI** thay đổi API Contract; không tăng API version và không cần tạo revision mới.
   - **Cấp độ 4: Thay đổi phá vỡ tương thích (Breaking Change):**
     *Hành vi:* Đổi tên trường bắt buộc, xóa trường, thay đổi kiểu dữ liệu, thay đổi HTTP semantics làm phá vỡ cam kết mà consumer hiện tại đang dựa vào.
     *Xử lý:* Đánh dấu contract cũ là `Deprecated`, khởi tạo file contract mới cho **API Version mới** (ví dụ: route `/api/v2/`, file `...-v2.contract.md` với **Document Revision khởi đầu là `v1.0`** cho hợp đồng API v2 đó). Không áp đặt con số revision phải trùng với số version API.
3. **Ghi chép Change Log:** Mọi lần cập nhật bắt buộc phải ghi 1 dòng mới vào bảng **Lịch sử thay đổi (Revision History)** ở đầu tài liệu contract.
