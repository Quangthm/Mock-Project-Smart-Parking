# [Tên Tính năng / Endpoint] — API Contract Specification (Operation Service)

> **Feature Key:** `[Mã Jira, VD: SPARK-233]`  
> **Service Owner:** `Operation Service`  
> **Target Interface / Route:** `[HTTP Route, VD: /api/v1/lots/{siteId}/policies/...]`  
> **Document Revision:** `v1.0` *(Bắt đầu từ v1.0)*  
> **API Version:** `[v1 | N/A | TBD]`  
> **Contract Status:** `[Backlog | Draft | In Review | Approved | Deprecated]`  
> **Communication Type:** `Client-to-Backend HTTP REST`  
> **Author:** `[Tên tác giả]`  
> **Reviewer:** `[Tên Reviewer / Auditor]`  
> **SRS / BR Baseline:** `SRS v1.1, BR-POL-01..04, policy-catalogue v1.1`  

---

### Lịch sử thay đổi (Revision History / Change Log) `[Required]`

| Revision | Ngày | Tác giả | Người duyệt | Loại thay đổi | Mô tả thay đổi / Jira Key |
| :---: | :---: | :--- | :--- | :--- | :--- |
| `v1.0` | `YYYY-MM-DD` | `[Author]` | `[Reviewer]` | Initial | Khởi tạo tài liệu đặc tả ban đầu theo task `[Jira-Key]` |

---

## 1. Tổng quan & Phạm vi nghiệp vụ (Overview & Business Scope) `[Required]`

### 1.1 Mục đích nghiệp vụ (Business Objective) `[Required]`
[Mô tả mục đích của API Contract này trong việc quản lý vận hành bãi đỗ xe hoặc đặt chỗ.]

### 1.2 Ranh giới & Consumer (Consumer & Boundary Scope) `[Required]`
- **Bên tiêu thụ (Consumer / Caller):** [Owner Web Portal, Driver Mobile App, Internal Checkout Orchestrator...]
- **Service sở hữu (Owning Service):** `Operation Service`
- **Ranh giới không xâm phạm:** Không quản lý bảng giá (Payment Service làm chủ).

### 1.3 Xác thực & Phân quyền (Authentication & Authorization) `[Required]`
- **Cơ chế xác thực:** `Bearer JWT`
- **Quyền hạn bắt buộc:** `LOT_POLICY_MANAGE` (đối với Owner cấu hình) hoặc quyền tương ứng.
- **Phạm vi tài nguyên (Resource Scope):** Kiểm tra chặt chẽ `owned-lot scope` (ngăn chặn truy cập chéo bãi `Cross-lot access`).

### 1.4 Chính sách Idempotency (Idempotency Policy) `[Conditional]`
- **Header quy chuẩn:** `Idempotency-Key` (UUID v4 do Client sinh ra).
- **Phạm vi duy nhất:** `Scope = (tenant_id, site_id, endpoint_route, idempotency_key)`.
- **Hành vi xử lý:** Trả về kết quả replayed nếu trùng key và payload; trả về `409 Conflict` nếu trùng key nhưng khác payload.

### 1.5 Bảng truy vết yêu cầu hai chiều (Bi-directional Traceability) `[Required]`

| Mã Yêu cầu | Tài liệu nguồn | Mô tả yêu cầu nghiệp vụ | Ánh xạ trong API Contract |
| :--- | :--- | :--- | :--- |
| `FR-POL-01` | SRS v1.1 | Owner cấu hình chính sách trong giới hạn Admin | Endpoints PUT /draft, POST /publish |
| `FR-POL-03` | SRS v1.1 | Cung cấp danh mục chính sách typed, scoped kèm units | Endpoint GET /catalogue |
| `FR-POL-04` | SRS v1.1 | Validate khoảng giá trị và từ chối cấu hình sai phạm | Error matrix 400 Out-of-bounds |
| `FR-POL-05` | SRS v1.1 | Lưu vết revision và snapshot cho booking đã chấp thuận | Endpoint GET /revisions |
| `FR-POL-07` | SRS v1.1 | Hierarchy resolution: Admin bounds -> Owner override | DTO effective_value resolution |
| `FR-POL-08` | SRS v1.1 | Quản lý Draft, Scheduled activation và Revision history | Endpoint PUT /draft, POST /publish |

---

## 2. Đặc tả Hợp đồng Giao diện (Interface Specification) `[Required]`

### 2.1 Danh sách Endpoints
[Bảng tổng hợp HTTP Method, Path, Mô tả, Quyền]

### 2.2 Chi tiết từng Endpoint
[Request payload schema, Response schema, Ví dụ JSON]

---

## 3. Quy tắc Nghiệp vụ & Invariants (Domain Invariants) `[Required]`

- Bảng danh mục chính sách (Keys, Data types, Units, Default values, Permitted bounds).
- State Machine của Policy Revision (`Draft` $\rightarrow$ `Scheduled` $\rightarrow$ `Active` $\rightarrow$ `Archived`).
- Quy tắc bảo toàn snapshot đối với các giao dịch trong quá khứ (`BR-POL-03`).

---

## 4. Ma trận Mã lỗi & Xử lý ngoại lệ (Error Catalogue) `[Required]`

[Bảng mã lỗi chuẩn HTTP Status, Error Code, Nguyên nhân và Hướng dẫn khắc phục]

---

## 5. Yêu cầu Phi chức năng & Kiểm thử (NFRs & Verification) `[Required]`

- SLA độ trễ: P95 $\le 200\text{ms}$.
- Structured Logging với `ILogger<T>`, không rò rỉ thông tin nhạy cảm.
- Bộ kịch bản Unit Tests & Integration Tests bắt buộc.
