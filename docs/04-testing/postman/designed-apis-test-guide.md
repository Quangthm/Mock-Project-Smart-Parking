# Hướng Dẫn Kiểm Thử Bộ API Thiết Kế Sprint 2 Bằng Postman

Tài liệu này hướng dẫn cách sử dụng bộ Postman Collection và Test Scripts tự động kiểm thử toàn bộ **7 API Specifications** do **Nguyễn Duy Khánh** thiết kế trong Sprint 2.

---

## 1. Danh Mục Các API Được Kiểm Thử

| # | Feature / Issue ID | API Design Specification | Controller / Endpoint Chính | Mô Tả & Nghiệp Vụ Kiểm Thử |
|---|--------------------|--------------------------|-----------------------------|----------------------------|
| 1 | **Driver Register** (SPARK-174/175) | `docs/03-design/api/driver-register-api-design.md` | `POST /api/auth/register/driver`<br>`POST /api/auth/register/driver/verify`<br>`POST /api/auth/register/driver/resend`<br>`POST /api/auth/register/driver/recover` | Đăng ký tài xế bằng email/phone, sinh OTP 6 số, xác thực kích hoạt tài khoản, resend cooldown (60s), recover hồ sơ pending. Không leak OTP ra response. |
| 2 | **Owner Register** (SPARK-175) | `docs/03-design/api/owner-register-api-design.md` | `POST /api/auth/register/owner`<br>`POST /api/auth/register/owner/verify`<br>`POST /api/auth/register/owner/resend`<br>`POST /api/auth/register/owner/recover` | Đăng ký chủ bãi xe với thông tin doanh nghiệp, xác thực Email OTP (Gate 1). Chuyển trạng thái sang `pending` chờ Admin duyệt (Gate 2). |
| 3 | **Admin Approve Owner** (SPARK-194) | `docs/03-design/api/admin-approve-owner-api-design.md` | `GET /api/owner-applications`<br>`GET /api/owner-applications/{id}`<br>`PATCH /api/owner-applications/{id}/review` | Admin xem danh sách đơn đăng ký, xem chi tiết và duyệt (`approved`) hoặc từ chối (`rejected`) kèm lý do review. Chặn Driver/Owner truy cập (403). |
| 4 | **Admin Manage Users** (SPARK-205) | `docs/03-design/api/admin-manage-users-api-design.md` | `GET /api/users`<br>`GET /api/users/{id}`<br>`PATCH /api/users/{id}/status` | Admin tra cứu danh sách người dùng phân trang, lọc theo `role` / `status` / `search`. Khóa tài khoản (`locked`) và mở khóa (`active`). Chặn tự khóa chính mình và bảo vệ thông tin mật (không lộ passwordHash). |
| 5 | **Owner Create Operator** (SPARK-190) | `docs/03-design/api/owner-create-operator-api-design.md` | `POST /api/users`<br>`GET /api/auth/deliveries/{id}`<br>`POST /api/auth/deliveries/{id}/retry`<br>`GET /api/operators`<br>`GET /api/operators/me`<br>`POST /api/auth/bootstrap-password` | Chủ bãi tạo tài khoản nhân viên vận hành gắn với bãi xe (`siteIds`) và danh sách quyền ủy quyền (`permissions`). Quản lý hàng đợi gửi email thông tin (`deliveries`) và link đổi mật khẩu lần đầu (`bootstrap-password`). |
| 6 | **Vehicle Registration** (SPARK-226) | `docs/03-design/api/vehicle-registration-api-design.md` | `GET /api/vehicles`<br>`POST /api/vehicles`<br>`PATCH /api/vehicles/{id}` | Tài xế đăng ký xe ô tô (`CAR`) và xe máy (`MOTORCYCLE`). Engine chuẩn hóa biển số tự động (`rawPlate` -> `canonicalPlate` viết hoa, loại bỏ khoảng trắng, dấu gạch ngang, dấu chấm). Ràng buộc link ảnh HTTPS. |
| 7 | **Parking Structure** (SPARK-187) | `docs/03-design/api/parking-structure-api-design.md` | `GET/POST /api/parking-lots`<br>`GET/PATCH /api/parking-lots/{id}/structure`<br>`POST /api/parking-lots/{id}/units`<br>`POST /api/parking-lots/{id}/slots`<br>`POST /api/parking-lots/{id}/paths`<br>`POST /api/parking-lots/{id}/operations`<br>`GET /api/parking-lots/{id}/operations/{opId}` | Khởi tạo bãi xe, cấu trúc không gian phân cấp (`ZONE`, `FLOOR`), tạo slot đỗ (`CAR`, `STANDARD`), tạo luồng di chuyển (`paths`), cấu hình quota dự phòng (`backup`), đánh dấu slot khẩn cấp và thực thi structure operations an toàn (idempotent). |

---

## 2. Thông Tin Postman Collection File

- **Đường dẫn file:** `docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json`
- **Chuẩn định dạng:** Postman Collection Format v2.1.0
- **Tổng số Request:** 51 requests
- **Tổng số Test Script:** 51 test scripts (100% request đều có script assert và lưu biến chuỗi tự động)

---

## 3. Cấu Trúc Các Thư Mục (Folders)

```text
SmartPark - Sprint 2 Designed APIs Test Suite
├── 00 Authentication & Sessions (5 requests: Admin, Owner, Driver login, Me, Anonymous 401)
├── 01 Driver Registration (SPARK-174/175) (6 requests: Happy path, Bad email, Weak pass, Resend, Recover, Bad OTP)
├── 02 Owner Registration (SPARK-175) (4 requests: Happy path, Missing business info, Resend, Recover)
├── 03 Admin Approve Owner (SPARK-194) (4 requests: List apps, App detail, Review approve, Driver 403)
├── 04 Admin Manage Users (SPARK-205) (8 requests: Paginated search, Bad query, User detail, Lock, Unlock, Bad status, Driver 403)
├── 05 Owner Create Operator (SPARK-190) (6 requests: Create operator, Bad permission, Delivery status, Delivery retry, List ops, Bootstrap negative)
├── 06 Vehicle Registration & Normalization (SPARK-226) (6 requests: List vehicles, CAR normalization, MOTORCYCLE normalization, Update vehicle, Insecure HTTP, Invalid category)
├── 07 Parking Structure Management (SPARK-187) (10 requests: List lots, Create lot, Read structure & capacity, Create zone, Create slot, Create path, Rename unit, Backup quota, Safe operation, Poll operation)
└── 08 Logout & Teardown (2 requests: Driver logout, Revoked token 401)
```

---

## 4. Biến Collection (Variables) & Cơ Chế Chuỗi Dữ Liệu Tự Động

Collection sử dụng các biến sau (đã được cấu hình mặc định sẵn trong file JSON):

| Tên Biến | Giá Trị Mặc Định | Mô Tả |
|----------|-------------------|-------|
| `userUrl` | `http://localhost:5035` | URL của UserService API |
| `parkingUrl` | `http://localhost:5045` | URL của ParkingService API |
| `adminEmail` | `admin@gmail.com` | Email quản trị viên seed sẵn |
| `adminPassword` | `Password@123` | Mật khẩu tài khoản seed |
| `ownerEmail` | `owner@gmail.com` | Email chủ bãi xe seed sẵn |
| `driverEmail` | `driver@gmail.com` | Email tài xế seed sẵn |
| `adminToken` | *(Tự động lưu sau login)* | JWT Bearer token của Admin |
| `ownerToken` | *(Tự động lưu sau login)* | JWT Bearer token của Owner |
| `driverToken` | *(Tự động lưu sau login)* | JWT Bearer token của Driver |
| `operatorToken` | *(Tự động lưu sau login)* | JWT Bearer token của Operator |
| `registrationId` | *(Tự động lưu sau register)* | ID đăng ký của Driver để test verify/resend |
| `ownerApplicationId`| *(Tự động lưu sau register)* | ID hồ sơ đăng ký Owner |
| `ownerEmailChallenge`| *(Tự động lưu sau register)*| Challenge ID xác minh Email OTP |
| `targetUserId` | *(Tự động lưu sau search)* | User ID dùng để test Lock / Unlock trong Admin Manage Users |
| `operatorId` | *(Tự động lưu sau create)* | ID nhân viên vận hành vừa tạo |
| `deliveryId` | *(Tự động lưu sau create)* | ID bản ghi delivery onboarding email |
| `siteId` | *(Tự động lưu sau create lot)*| ID bãi đỗ xe vừa tạo |
| `unitId` | *(Tự động lưu sau create unit)*| ID khu vực đỗ xe (Zone) |
| `slotId` | *(Tự động lưu sau create slot)*| ID vị trí đỗ xe (Slot) |
| `vehicleId` | *(Tự động lưu sau create car)*| ID phương tiện đã đăng ký |
| `operationId` | *(Tự động lưu sau op)* | ID tác vụ structure operation hai pha |

> [!TIP]
> Các request tạo mới (Register Driver, Register Owner, Create Lot, Create Vehicle) sử dụng `{{$timestamp}}` và `{{$randomInt}}` tự động trong pre-request/body nên có thể chạy lặp lại nhiều lần mà **không bao giờ bị lỗi trùng lặp dữ liệu (Duplicate Conflict 409)**.

---

## 5. Hướng Dẫn Chạy Kiểm Thử

### Cách 1: Chạy Bằng Postman App (Desktop GUI)

1. Mở Postman Desktop.
2. Chọn **File** -> **Import** (hoặc tổ hợp `Ctrl + O`).
3. Chọn file `docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json`.
4. Đảm bảo Backend UserService (port 5035) và ParkingService (port 5045) đang chạy.
5. Nhấp chuột phải vào collection **SmartPark - Sprint 2 Designed APIs Test Suite** -> Chọn **Run collection**.
6. Chọn thứ tự chạy tuần tự và nhấn nút **Run SmartPark...**.
7. Xem kết quả chi tiết từng assertion trong tab **Test Results**.

### Cách 2: Chạy Tự Động Bằng Newman CLI

Trong terminal tại thư mục gốc repository, chạy lệnh:

```bash
npx newman run docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json
```

Hoặc xuất kết quả chi tiết ra màn hình console:

```bash
npx newman run docs/04-testing/postman/SmartParking-Designed-APIs.postman_collection.json --reporters cli
```

---

## 6. Tiêu Chí Nghiệm Thu (Assertion Strategies)

1. **HTTP Status Code:** Kiểm tra chính xác status code mong đợi (`200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`, `429 Too Many Requests`).
2. **Response Envelope:** Xác minh chuẩn response `{ success: true/false, data, code, message }`.
3. **Data Integrity & Schema:**
   - Các trường ID là chuẩn UUID (`/^[0-9a-f-]{36}$/i`).
   - Chuẩn hóa biển số xe (`canonicalPlate` không chứa khoảng trắng, gạch nối, chấm câu và viết hoa toàn bộ).
   - Tuyệt đối không để lộ thông tin nhạy cảm (`passwordHash`, `token`, `otp code`) trong các response danh sách hoặc chi tiết.
4. **Bảo Vệ Phân Quyền (RBAC):**
   - Tài xế gọi endpoint Admin (`/api/users`, `/api/owner-applications/{id}/review`) bị trả về `403 Forbidden`.
   - Gọi API bảo vệ không có Bearer token trả về `401 Unauthorized`.
   - Sau khi gọi `/api/auth/logout`, token cũ bị thu hồi và không thể truy cập lại `/api/auth/me`.
