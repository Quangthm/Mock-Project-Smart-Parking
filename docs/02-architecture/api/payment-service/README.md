# Payment Service — API Contracts & Specifications

> **Domain Boundary**: Financial & Payment Management  
> **Database Scope**: `smartpark_payment` (`04-payment-service-db.sql`)  
> **SRS Baseline**: v0.9 (2026-10-05)  
> **Standard Statuses**: `Backlog` | `Draft` | `In Review` | `Approved` | `Deprecated`  

---

## 1. Tiêu chuẩn & Quy trình (Standards & Workflows)

Mọi API Contract thuộc Payment Service phải tuân thủ nghiêm ngặt quy trình và biểu mẫu chuẩn công nghiệp sau:

- **[00-api-contract-workflow.md](00-api-contract-workflow.md)**: Quy trình chuẩn SOP 6 bước thiết kế, kiểm toán độc lập (Independent Peer Review), tính bất biến theo phiên bản (Version Immutability) và quản lý vòng đời API Contract.
- **[00-api-contract-template.md](00-api-contract-template.md)**: Khung biểu mẫu chuẩn (Standard Template) hỗ trợ cả 2 biến thể: Tính toán tài chính nội bộ (`Calculation Variant`) và Tích hợp cổng thanh toán / Webhook (`Integration Variant`), kèm phân định nguồn sự thật OpenAPI 3.1, NFRs và cấu trúc lỗi chuẩn Enterprise.

> **Ghi chú về Vai trò Baseline:** Bộ tài liệu này đóng vai trò là **Baseline chuẩn mực kỹ thuật** để bắt đầu áp dụng cho Payment Service và làm khung tham chiếu cho các service khác. Chuẩn hóa nhằm giúp các thành viên cộng tác nhất quán, nhận diện rõ điều chưa biết và ghi nhận câu hỏi mở (`[OPEN QUESTION]`) lên cấp có thẩm quyền, không cố gắng áp đặt mọi chi tiết kỹ thuật trước khi có đủ dữ liệu thực tế.

---

## 2. Danh mục API Contracts (Active Contracts Catalogue)

| STT | Mã Jira | Tên Contract / Tính năng | API Version | Document Revision | Loại giao tiếp | Trạng thái | Đường dẫn tài liệu |
| :---: | :---: | :--- | :---: | :---: | :--- | :---: | :--- |
| **01** | `SPARK-229` | **Parking Pricing Calculator** (Core Rules & Contract) | `N/A` *(Internal)* | `v1.1` | Internal Domain Interface *(Companion HTTP Deferred)* | `Approved` | [01-pricing-calculation.contract.md](01-pricing-calculation.contract.md) |
| **02** | `SPARK-247` | **Payment Gateway Integration** (VNPay, MoMo, ZaloPay, Cash) | `TBD` | `v1.0` | HTTP REST + Webhook Callback | `Backlog` | *(Kế hoạch Sprint tiếp theo)*

> **Quy ước:** `API Version` chỉ áp dụng cho route do SmartPark quản lý khi sử dụng URL/API versioning; `Document Revision` là phiên bản tài liệu contract. Với contract chưa xác định route/version, ghi `TBD`; không suy ra API Version từ Document Revision. Với tích hợp outbound sang Provider, version của API Provider do chính Provider quản lý.
