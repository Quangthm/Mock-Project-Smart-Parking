# BÁO CÁO HOÀN THÀNH POSTMAN TEST SCRIPTS CHO CÁC API SPRINT 2

> **File Collection:** `docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json`

---

## PHẦN 1: MẪU TIN NHẮN GỬI NHANH LÊN ZALO (COPY & PASTE)

```text
 đã viết xong bộ Postman Collection & Test Scripts tự động kiểm thử toàn bộ 7 API, đã thiết kế trong Sprint 2 rồi

📌 Thông tin bộ Test:
- File Collection: docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json
- Tổng số requests: 51 requests (100% đều có test script kiểm tra status code, schema envelope và business rules).
- Cấu trúc 9 folder bao quát 7 API designs:
  1. Driver Register & OTP (SPARK-174/175): Check UUID, ẩn OTP khỏi response, resend cooldown (60s), recover hồ sơ.
  2. Owner Register & Email OTP (SPARK-175): Check Gate 1 OTP, trạng thái pending chờ duyệt, validate thông tin doanh nghiệp.
  3. Admin Approve Owner (SPARK-194): Duyệt/từ chối hồ sơ kèm note audit, chặn Driver/Owner truy cập trái phép (403).
  4. Admin Manage Users (SPARK-205): Tìm kiếm phân trang, lọc role/status, khóa (locked) & mở khóa (active), bảo mật không lộ hash/credential.
  5. Owner Create Operator (SPARK-190): Tạo Operator gán siteIds & permissions, kiểm tra tracking email outbox, retry delivery, link bootstrap password.
  6. Vehicle Registration (SPARK-226): Đăng ký xe CAR & MOTORCYCLE, kiểm tra engine chuẩn hóa biển số (rawPlate -> canonicalPlate viết hoa bỏ dấu), bắt buộc HTTPS.
  7. Parking Structure (SPARK-187): Tạo bãi, zone, slot, luồng di chuyển, công thức tính capacity breakdown, backup quota và structure operations 2 pha an toàn.
  8. Auth Sessions & Logout: Luồng token chuỗi tự động, thu hồi token sau logout (revoked token trả về 401).

💡 Điểm tối ưu:
- Các request tạo mới dùng dynamic timestamp/random nên có thể chạy lặp lại nhiều lần mà không bị lỗi trùng dữ liệu (409 Conflict).
- Đã cover đầy đủ cả Happy Path lẫn Negative / Boundary Cases (400, 401, 403, 404, 409, 429).

🚀 Lệnh chạy tự động bằng Newman CLI:
npx newman run docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json

Anh và team import file JSON trên vào Postman Desktop để review và test nhé ạ!
```

---

## PHẦN 2: BÁO CÁO KỸ THUẬT CHI TIẾT (ĐÍNH KÈM)

### 1. Bảng Ánh Xạ Chi Tiết 7 API Specifications Đã Thiết Kế

| #   | Nhóm Chức Năng            | API Spec Tài Liệu                                        | Endpoints Trọng Tâm                                                                                                                                                                                                                                                                             | Logic & Assertion Được Kiểm Thử Trong Scripts                                                                                                                                                                                                                                                                                                                                                              |
| --- | ------------------------- | -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Driver Register**       | `docs/03-design/api/driver-register-api-design.md`       | `POST /api/auth/register/driver`<br>`POST /api/auth/register/driver/verify`<br>`POST /api/auth/register/driver/resend`<br>`POST /api/auth/register/driver/recover`                                                                                                                              | - Sinh `registrationId` dạng UUID.<br>- Xác thực response không để lộ mã OTP (`data.code`).<br>- Kiểm tra lỗi 400 khi email sai cú pháp hoặc mật khẩu yếu.<br>- Kiểm tra resend cooldown (60s) trả về `resendAvailableAt` hoặc mã 429.<br>- Kiểm tra recover trạng thái hồ sơ đang chờ xác minh.                                                                                                           |
| 2   | **Owner Register**        | `docs/03-design/api/owner-register-api-design.md`        | `POST /api/auth/register/owner`<br>`POST /api/auth/register/owner/verify`<br>`POST /api/auth/register/owner/resend`<br>`POST /api/auth/register/owner/recover`                                                                                                                                  | - Đăng ký kèm tên doanh nghiệp, loại bãi (`outdoor/basement/multi-storey`).<br>- Tự động lưu `ownerApplicationId` và `ownerEmailChallenge`.<br>- Bắt lỗi 400 khi thiếu thông tin bắt buộc (ví dụ: `businessName`).<br>- Xác thực Gate 1 qua email OTP, chuyển trạng thái sang `pending` chờ Admin duyệt.                                                                                                   |
| 3   | **Admin Approve Owner**   | `docs/03-design/api/admin-approve-owner-api-design.md`   | `GET /api/owner-applications`<br>`GET /api/owner-applications/{id}`<br>`PATCH /api/owner-applications/{id}/review`                                                                                                                                                                              | - Admin lấy danh sách hồ sơ đăng ký.<br>- Xem chi tiết hồ sơ theo ID trích xuất tự động.<br>- Submit review cập nhật `status: approved` kèm `reviewNote`.<br>- Kiểm tra phân quyền: Driver gọi API review bị trả về **403 Forbidden**.                                                                                                                                                                     |
| 4   | **Admin Manage Users**    | `docs/03-design/api/admin-manage-users-api-design.md`    | `GET /api/users`<br>`GET /api/users/{id}`<br>`PATCH /api/users/{id}/status`                                                                                                                                                                                                                     | - Tìm kiếm phân trang (`page`, `pageSize`, lọc theo `role`, `status`).<br>- Assert bảo mật: response JSON tuyệt đối không chứa `passwordHash` hay credentials.<br>- Bắt lỗi 400 khi query parameter không hợp lệ (`page=0`, `pageSize=101`).<br>- Thực hiện khóa tài khoản (`status: locked`) và mở khóa (`status: active`).<br>- Kiểm tra phân quyền: tài xế truy cập bị chặn với **403 Forbidden**.      |
| 5   | **Owner Create Operator** | `docs/03-design/api/owner-create-operator-api-design.md` | `POST /api/users`<br>`GET /api/auth/deliveries/{id}`<br>`POST /api/auth/deliveries/{id}/retry`<br>`GET /api/operators`<br>`GET /api/operators/me`<br>`POST /api/auth/bootstrap-password`                                                                                                        | - Chủ bãi tạo Operator kèm danh sách bãi được cấp quyền (`siteIds`) và permissions (`SLOT_OVERRIDE`, `DEVICE_MANAGE`).<br>- Bắt lỗi khi truyền permission trái phép ngoài allowlist.<br>- Trích xuất và kiểm tra trạng thái email outbox qua `deliveryId`.<br>- Kiểm tra gọi retry delivery không làm trùng lặp tài khoản.<br>- Kiểm tra chống giả mạo / token không hợp lệ ở endpoint bootstrap password. |
| 6   | **Vehicle Registration**  | `docs/03-design/api/vehicle-registration-api-design.md`  | `GET /api/vehicles`<br>`POST /api/vehicles`<br>`PATCH /api/vehicles/{id}`                                                                                                                                                                                                                       | - Đăng ký ô tô (`CAR`): verify `rawPlate = "51A-xxx.99"` được chuẩn hóa tự động thành `canonicalPlate = "51Axxx99"`.<br>- Đăng ký xe máy (`MOTORCYCLE`): verify loại bỏ khoảng trắng và dấu gạch nối.<br>- Bắt lỗi 400 khi truyền URL ảnh không phải HTTPS (`http://...`).<br>- Bắt lỗi 400 khi truyền loại phương tiện không hỗ trợ (`BICYCLE`).                                                          |
| 7   | **Parking Structure**     | `docs/03-design/api/parking-structure-api-design.md`     | `GET/POST /api/parking-lots`<br>`GET/PATCH /api/parking-lots/{id}/structure`<br>`POST /api/parking-lots/{id}/units`<br>`POST /api/parking-lots/{id}/slots`<br>`POST /api/parking-lots/{id}/paths`<br>`POST /api/parking-lots/{id}/operations`<br>`GET /api/parking-lots/{id}/operations/{opId}` | - Khởi tạo bãi xe với tọa độ GPS và mã duy nhất sinh tự động.<br>- Xác thực cấu trúc phân cấp không gian (`ZONE`, `FLOOR`) và tạo slot vật lý.<br>- Cấu hình quota dự phòng (`backup capacity`) cho xe ô tô.<br>- Kiểm tra cấu trúc layout hiển thị trên màn hình Operator.<br>- Thực thi Structure Operation 2 pha có `idempotencyKey` an toàn, không evict xe đang đỗ.                                   |

---

### 2. Cấu Trúc Biến Tự Động (Chaining Variables)

Collection đã thiết lập cơ chế chuyển tiếp dữ liệu mượt mà giữa các bước:

```text
Admin/Owner/Driver Login
  │──> Lưu adminToken, ownerToken, driverToken
  │
  ├──> [Owner Register] ──> Lưu ownerApplicationId & ownerEmailChallenge
  │                           │
  │                           └──> [Admin Approve] ──> Duyệt hồ sơ ownerApplicationId
  │
  ├──> [Admin Search Users] ──> Lưu targetUserId
  │                               │
  │                               └──> [Admin Set Status] ──> Khóa / Mở khóa targetUserId
  │
  ├──> [Create Parking Lot] ──> Lưu siteId
  │                               ├──> [Create Spatial Unit] ──> Lưu unitId
  │                               │                                 │
  │                               │                                 └──> [Create Slot] ──> Lưu slotId
  │                               └──> [Owner Create Operator] ──> Gán siteId, lưu operatorId & deliveryId
  │
  ├──> [Register Vehicle] ──> Lưu vehicleId ──> [Update Vehicle]
  │
  └──> [Driver Logout] ──> [Verify Revoked Token] ──> Chặn token cũ (401)
```

---

### 3. Hướng Dẫn Sử Dụng

#### Chạy qua Postman GUI

1. Mở Postman Desktop, bấm nút **Import** ở góc trên bên trái.
2. Chọn file `docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json`.
3. Kiểm tra biến môi trường:
   - `userUrl`: `http://localhost:5035`
   - `parkingUrl`: `http://localhost:5045`
4. Bấm chuột phải vào Collection -> Chọn **Run collection** -> Bấm **Run SmartPark - Sprint 2 Designed APIs Test Suite**.

#### Chạy qua Command Line (Newman)

Chạy trực tiếp từ terminal tại thư mục gốc của project:

```bash
npx newman run docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json --reporters cli
```
