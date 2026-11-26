# OperationService Postman Testing Guide

Updated 2026-11-12. Use this guide with:

- [Operation collection](OperationService.postman_collection.json)
- [Operation LOCAL environment template](OperationService-Local.postman_environment.example.json)
- [Owner Lot Policies API Contract](../../02-architecture/api/operation-service/01-owner-parking-lot-policies.contract.md)
- [Policy Catalogue](../../01-requirements/business-rules/policy-catalogue.md)

---

## 1. Nguyên nhân các lỗi Fail ở lần chạy trước & Bản sửa đổi

Khi chạy bộ test lần đầu, bạn gặp hàng loạt lỗi `401 Unauthorized` và `400 Bad Request` do 2 nguyên nhân cốt lõi:

1. **Chưa có Bearer Token trong Environment (`401 UNAUTHORIZED`):**
   - File template `OperationService-Local.postman_environment.example.json` để trống `ownerToken: ""` và `otherOwnerToken: ""`.
   - Khi bấm **Run collection** ngay, toàn bộ các request đòi hỏi quyền Owner (`1.1` đến `1.5`, `1.8` đến `1.10`) gửi header rỗng nên bị backend chặn lập tức với `401 UNAUTHORIZED: Sign in as an owner.`.
   - **Giải pháp mới:** Đã bổ sung request **`0.2 Login as Owner (Auto Token)`** vào thư mục `00 - Setup & Authentication`. Khi chạy từ đầu, request này sẽ tự động gọi `UserService` (`http://localhost:5035/api/auth/login`) với tài khoản Owner đã seed sẵn (`owner@smartpark.local` / `Aa1!RKJ7NEyy`) và tự động lưu `ownerToken` vào biến môi trường!

2. **Hiện tượng đè biến Postman (`Variable Shadowing` gây lỗi `400` tại 1.6 & 1.7):**
   - Trong Postman, biến ở cấp **Environment** có độ ưu tiên cao hơn biến cấp **Collection**.
   - Do file Environment ban đầu có khai báo `idempotencyKey: ""` (rỗng), khi Pre-request script sinh UUID vào Collection variable, Postman vẫn lấy giá trị rỗng từ Environment để gửi đi $\rightarrow$ Backend bắt lỗi thiếu header `Idempotency-Key` (400 Bad Request).
   - **Giải pháp mới:** 
     - Loại bỏ `idempotencyKey` ra khỏi file Environment (theo chuẩn thiết kế của `PaymentService`).
     - Script Pre-request cập nhật song song vào cả `pm.collectionVariables` lẫn `pm.environment`.

3. **Cập nhật chính xác Assertion theo Response thật của Backend:**
   - `GET /catalogue`: Trả về mảng `data.policies` (15 tham số vận hành).
   - `PUT /draft`: Trả về `data.status: "DRAFT"`.
   - `GET /draft`: Trả về `data.policies.{tênThuộcTính}`.
   - `POST /publish`: Trả về `data.status: "ACTIVE"`, `data.newRevision`.
   - `GET /revisions`: Trả về `data.items` chứa danh sách các revision.

---

## 2. Các dịch vụ cần khởi động trước khi chạy

Đảm bảo cả 2 microservices sau đang chạy trên máy local:
1. **UserService:** `http://localhost:5035` (dùng để xác thực Owner và cấp Bearer JWT).
2. **OperationService:** `http://localhost:5055` (chứa các API chính sách bãi xe).

Khởi động qua Docker Compose hoặc PowerShell runner:
```powershell
# Khởi động UserService và OperationService
.\scripts\run-app-service.ps1 -Service User -NoBuild
.\scripts\run-app-service.ps1 -Service Operation -NoBuild
```

---

## 3. Cách Import và Chạy lại trên Postman

1. **Re-import Collection & Environment:**
   - Trong Postman, chọn **Import** $\rightarrow$ Chọn lại 2 file mới:
     - `docs/04-testing/postman/OperationService.postman_collection.json` (chọn **Replace** khi được hỏi).
     - `docs/04-testing/postman/OperationService-Local.postman_environment.example.json`.
2. **Chọn Environment:**
   - Ở góc trên bên phải của Postman, chọn đúng môi trường: **SmartParking Operation LOCAL - Owner Lot Policies**.
3. **Chạy toàn bộ Suite bằng Collection Runner:**
   - Click chuột phải vào Collection `SmartParking - OperationService` $\rightarrow$ Chọn **Run collection**.
   - Đảm bảo các folder được chọn theo đúng thứ tự:
     - `00 - Setup & Authentication` (Request `0.2` sẽ tự động đăng nhập và gán token).
     - `01 - Owner Lot Policies (SPARK-230)`.
   - Bấm nút **Run SmartParking - OperationService**.

---

## 4. Bảng kỳ vọng kết quả từng request (18 Requests)

| STT | Tên Request | Method & URL | Kỳ vọng HTTP | Dữ liệu kiểm tra chính |
| :--- | :--- | :--- | :--- | :--- |
| **0.1** | Health Check | `GET /health` | **200 OK** | Dịch vụ sẵn sàng |
| **0.2** | Login as Owner (Auto Token) | `POST /api/auth/login` | **200 OK** | Tự động lấy `ownerToken` và lưu vào environment |
| **1.1** | Get Catalogue - Happy Path | `GET /policies/catalogue` | **200 OK** | Trả về 15 policies vận hành |
| **1.2** | Get Draft - Initial State | `GET /policies/draft` | **200 OK** | Trả về thông tin draft |
| **1.3** | Save Draft - Envelope Policies | `PUT /policies/draft` | **200 OK** | `data.status == "DRAFT"` |
| **1.4** | Save Draft - Flat Fields | `PUT /policies/draft` | **200 OK** | `data.status == "DRAFT"` |
| **1.5** | Get Draft - With Saved Data | `GET /policies/draft` | **200 OK** | `paymentHoldDurationMinutes == 10` |
| **1.6** | Publish Policy - Immediate Activation | `POST /policies/publish` | **200 OK** | `status: "ACTIVE"`, lưu `publishedRevision` |
| **1.7** | Publish Policy - Idempotency Replay | `POST /policies/publish` | **200 OK** | Header `Idempotency-Replayed: true` |
| **1.8** | Publish Policy - Scheduled Future Activation | `POST /policies/publish` | **200 OK** | `status: "ACTIVE"` hoặc `"SCHEDULED"` |
| **1.9** | Get Policy Revisions History | `GET /policies/revisions` | **200 OK** | `data.items` không rỗng |
| **1.10** | Get Specific Revision Snapshot | `GET /policies/revisions/{rev}` | **200 OK** | Snapshot đúng revision vừa publish |
| **1.11** | Auth Failure - Missing Token | `GET /policies/catalogue` | **401 Unauthorized** | `code: "UNAUTHORIZED"` |
| **1.12** | Auth Failure - Cross-Lot Access Denied | `GET /policies/catalogue` | **Skip hoặc 403** | Tự động skip nếu chưa cấu hình `otherOwnerToken` |
| **1.13** | Error Case - Site Not Found | `GET /policies/catalogue` (sai siteId) | **404 Not Found** | `code: "SITE_NOT_FOUND"` |
| **1.14** | Validation Error - Parameter Exceeds Ceiling | `PUT /policies/draft` (999m) | **400 Bad Request** | `code: "POLICY_OUT_OF_BOUNDS"` |
| **1.15** | Validation Error - Min > Max Duration | `PUT /policies/draft` (180m > 2h) | **400 Bad Request** | `code: "INVALID_TIME_DURATION"` |
| **1.16** | Validation Error - Past Activation Schedule | `POST /policies/publish` (quá khứ) | **400 Bad Request** | `code: "INVALID_ACTIVATION_SCHEDULE"` |

---

## 5. Xử lý sự cố (Troubleshooting)

| Lỗi | Nguyên nhân | Cách khắc phục |
| :--- | :--- | :--- |
| **0.2 Login ECONNREFUSED 5035** | `UserService` chưa chạy | Chạy `.\scripts\run-app-service.ps1 -Service User -NoBuild` hoặc `docker compose up -d user-service`. |
| **0.1 ECONNREFUSED 5055** | `OperationService` chưa chạy | Chạy `.\scripts\run-app-service.ps1 -Service Operation -NoBuild`. |
| **1.12 hiển thị Skip** | Chưa có token của Owner thứ 2 | Đây là hành vi bình thường, Postman sẽ ghi log `SKIP 1.12` mà không gây fail run test. |
