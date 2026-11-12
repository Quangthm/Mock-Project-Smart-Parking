# Operation Service — API Contracts & Specifications

> **Domain Boundary**: Parking Operations, Reservation & Operational Policy Management  
> **Database Scope**: `smartpark_operations` (`03b-reservation-operations-service-db.sql`)  
> **SRS Baseline**: v1.1  
> **Standard Statuses**: `Backlog` | `Draft` | `In Review` | `Approved` | `Deprecated`  

---

## 1. Tiêu chuẩn & Quy trình (Standards & Workflows)

Mọi API Contract thuộc Operation Service phải tuân thủ nghiêm ngặt quy trình và biểu mẫu chuẩn của dự án:

- **[00-api-contract-workflow.md](00-api-contract-workflow.md)**: Quy trình chuẩn SOP 6 bước thiết kế, kiểm toán độc lập (Independent Peer Review), tính bất biến theo phiên bản (Version Immutability) và quản lý vòng đời API Contract.
- **[00-api-contract-template.md](00-api-contract-template.md)**: Khung biểu mẫu chuẩn (Standard Template) hỗ trợ cả 2 biến thể: Quản lý chính sách vận hành (`Policy & Configuration Variant`) và Điều phối vận hành / Đặt chỗ (`Operations & Reservation Variant`), kèm phân định nguồn sự thật OpenAPI 3.1, NFRs và cấu trúc lỗi chuẩn Enterprise.

> **Ghi chú về Ranh giới Sở hữu Dịch vụ (Service Boundaries):**
> - **Operation Service sở hữu độc quyền:** Toàn bộ tham số vận hành, thời gian cửa sổ giữ chỗ (`hold duration`), thời gian đỗ tối thiểu/tối đa, độ trễ check-in (`arrival tolerance`), cửa sổ bảo vệ (`protection window`), thời gian phân bổ trước (`allocation lead time`), giới hạn đặt trước, chính sách slot dự phòng (`backup capacity`), và cờ bật/tắt dịch vụ đỗ xe.
> - **Payment Service sở hữu độc quyền:** Biểu phí (`tariffs`), phương thức đặt cọc (`prepayment`), tỉ lệ hoàn tiền khi hủy chỗ (`cancellation refund rate`). Operation Service chỉ tham chiếu phiên bản chính sách, **tuyệt đối không duplicate bảng giá** và **không cross-DB**.

---

## 2. Danh mục API Contracts (Active Contracts Catalogue)

| STT | Mã Jira | Tên Contract / Tính năng | API Version | Document Revision | Loại giao tiếp | Trạng thái | Đường dẫn tài liệu |
| :---: | :---: | :--- | :---: | :---: | :--- | :---: | :--- |
| **01** | `SPARK-233` / `SPARK-230` | **Owner Configures Parking Lot Policies** (Catalogue, Draft, Publish & Revisions) | `v1` | `v1.0` | Client-to-Backend HTTP REST | `Approved` | [01-owner-parking-lot-policies.contract.md](01-owner-parking-lot-policies.contract.md) |

> **Quy ước:** `API Version` áp dụng cho HTTP route công khai (`/api/v1/lots/{siteId}/policies/...`); `Document Revision` là phiên bản tài liệu contract (bắt đầu từ `v1.0`).
