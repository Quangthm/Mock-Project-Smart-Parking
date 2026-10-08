# User Service — Database Documentation

> **Service**: User Service  
> **Database file**: [`01-user-service-db.sql`](../../../../scripts/database/microservices/01-user-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý định danh con người (`users`), tài khoản theo ngữ cảnh (`accounts`), phân quyền RBAC (`roles`, `account_roles`), phương tiện (`vehicles`), vòng đời token xác thực (`user_refresh_tokens`), đăng ký OTP tài xế (`driver_registrations`), đơn đăng ký chủ bãi (`owner_applications`), và phân quyền vận hành bãi (`operator_grants`).

---

## 1. Tổng quan kiến trúc

User Service là **service định danh và phân quyền trung tâm** của hệ thống SmartPark. Toàn bộ các service khác (Parking, Reservation, Payment…) đều tham chiếu đến `user_id` / `account_id` dưới dạng **Logical ID** — không có FK vật lý xuyên database.

```
Client → API Gateway (JWT RS256)
                 ↓
          User Service DB
          ┌───────────────┐
          │ users         │ ← Định danh con người
          │ accounts      │ ← Ngữ cảnh vận hành (tenant/site)
          │ roles         │ ← Danh mục vai trò
          │ account_roles │ ← Gán vai trò cho account
          │ vehicles      │ ← Phương tiện của driver
          │ user_refresh_tokens │ ← Token lifecycle
          │ driver_registrations│ ← OTP đăng ký
          │ owner_applications  │ ← Đơn chủ bãi
          │ operator_grants     │ ← Phân quyền vận hành
          └───────────────┘
```

**Nguyên tắc dữ liệu:**
- `users` = bản thể con người, tồn tại độc lập.
- `accounts` = bản thể vận hành, gắn với tenant/site, một `user` có thể có nhiều `account`.
- Tất cả cross-service references (đến Parking Service, Reservation Service…) sử dụng **Logical ID** lưu dưới dạng `UUID`, không enforce bằng FK vật lý.

---

## 2. Bảng `users` — Định danh con người

### 2.1 Mục đích nghiệp vụ

Lưu trữ **thông tin định danh vật lý của một con người** trong hệ thống. Đây là root entity của toàn bộ User Service — mọi `account`, `vehicle`, `refresh_token`, `driver_registration` và `owner_application` đều gắn vào một `user`. Bảng này giải quyết bài toán:
- Xác thực danh tính người dùng qua phone/email + password.
- Bảo vệ tài khoản khỏi brute-force bằng cơ chế lockout.
- Quản lý vòng đời tài khoản (ACTIVE → LOCKED → INACTIVE).
- Hỗ trợ soft-delete để giữ toàn vẹn dữ liệu lịch sử.

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-01** | C | Đăng ký và xác minh OTP theo §3.1.1; giá trị xác thực bảo mật bị ràng buộc cứng |
| **FR-AUTH-02** | C | Xác thực theo §3.1.4, bao gồm logout, session/renewal termination, từ chối yêu cầu với account bị khóa |
| **FR-AUTH-03** | C | Driver chỉ được sửa `full_name`, `avatar_url` qua profile-edit thông thường; thay `email`/`phone` phải dùng re-verification flow riêng; role/status không customer-editable |
| **FR-AUTH-06** | C | Với mỗi protected operation: kiểm tra current role/permission, resource scope, **và** current account access state — token hợp lệ không đủ nếu account bị lock |
| **FR-AUTH-05** | C | Admin duyệt Owner registration sau khi có đủ company name, email, phone |
| **BR-AUTH-01–03** | — | Scoped roles; confirmed profile-edit/contact/password boundaries |
| **§3.1.1** | — | OTP valid 5 phút; sau 3 lần nhập sai → khóa 15 phút |
| **§3.1.4** | — | Session token hết hạn 24h; refresh token 7 ngày |
| **§4.3** | NFR | bcrypt cost factor 12; AES-256 at rest; JWT RS256; lockout 15 phút / 3 lần thất bại |
| **US-D08** | US | "Là Driver, tôi muốn đăng ký và đăng nhập vào hệ thống" |
| **US-A01** | US | "Là Admin, tôi muốn quản lý tài khoản người dùng" |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của con người trong hệ thống; được dùng làm Logical ID ở toàn bộ các service khác | Tự sinh bởi `uuid_generate_v4()` tại thời điểm INSERT | FR-AUTH-01 |
| `phone` | `VARCHAR(20) NULL` | Số điện thoại dùng để đăng nhập và nhận OTP qua SMS. `NULL` khi user đăng ký bằng email. | Do người dùng nhập khi đăng ký; format theo chuẩn Việt Nam | FR-AUTH-01; §3.1.1 |
| `email` | `VARCHAR(255) NULL` | Email dùng để đăng nhập và nhận OTP. `NULL` khi user đăng ký bằng số điện thoại. | Do người dùng nhập; Owner bắt buộc phải có email (§3.1.1) | FR-AUTH-01; FR-AUTH-05; §3.1.1 |
| `password_hash` | `VARCHAR(255) NOT NULL` | Giá trị băm của mật khẩu người dùng. **Không bao giờ lưu plain-text.** | Băm bằng **bcrypt** với cost factor 12 (§4.3) tại lớp Application trước khi INSERT | §4.3 (Security) |
| `full_name` | `VARCHAR(255) NOT NULL` | Tên hiển thị của người dùng; được phép sửa qua profile-edit flow thông thường | Do người dùng nhập khi đăng ký hoặc cập nhật; không cần re-verification | FR-AUTH-03 |
| `company_name` | `VARCHAR(255) NULL` | Tên công ty/doanh nghiệp, **bắt buộc với Owner** khi đăng ký; `NULL` với Driver và Operator | Do Owner nhập trong quá trình đăng ký. Admin kiểm tra trường này trước khi approve (FR-AUTH-05) | FR-AUTH-05; §3.1.1 |
| `avatar_url` | `TEXT NULL` | URL ảnh đại diện của người dùng; được phép sửa qua profile-edit flow thông thường | URL do frontend upload lên storage (S3/CDN), sau đó lưu URL vào đây | FR-AUTH-03 |
| `failed_login_attempts` | `INT NOT NULL DEFAULT 0` | Đếm số lần nhập sai thông tin đăng nhập **liên tiếp**. Khi đạt ngưỡng 3, tài khoản bị khóa tạm thời. Reset về 0 sau mỗi lần đăng nhập thành công | Tăng 1 mỗi khi authentication thất bại; reset khi thành công | FR-AUTH-02; §3.1.1; §4.3 (lockout 3 lần) |
| `locked_until` | `TIMESTAMPTZ NULL` | Thời điểm kết thúc khóa tạm thời do brute-force. `NULL` khi tài khoản không bị khóa tạm. Sau thời điểm này, tài khoản tự động cho phép đăng nhập lại mà không cần Admin can thiệp | Gán = `NOW() + INTERVAL '15 minutes'` tại lớp Application khi `failed_login_attempts` đạt ngưỡng (§4.3) | FR-AUTH-02; §3.1.1; §4.3 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` CHECK IN (`'ACTIVE'`, `'INACTIVE'`, `'LOCKED'`, `'PENDING_APPROVAL'`) | Trạng thái vòng đời của tài khoản người dùng. `ACTIVE`: hoạt động bình thường. `INACTIVE`: bị vô hiệu hóa thủ công. `LOCKED`: bị khóa vĩnh viễn bởi Admin. `PENDING_APPROVAL`: Owner mới đăng ký chờ Admin duyệt | Mặc định `ACTIVE` khi tạo (trừ Owner registration → `PENDING_APPROVAL`). Chỉ Admin được thay đổi `status`, không phải customer | FR-AUTH-02; FR-AUTH-05; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi. Dùng cho audit và báo cáo | Tự sinh bởi database tại thời điểm INSERT | §4.3 (Audit); FR-AUTH-05 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật bản ghi lần cuối. Cần được cập nhật thủ công bởi Application mỗi khi có UPDATE | Application cập nhật = `NOW()` tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | **Soft-delete**: thời điểm xóa logic. `NULL` = chưa xóa; có giá trị = đã xóa. Cho phép giữ lại lịch sử và toàn vẹn dữ liệu tham chiếu | Application gán = `NOW()` thay vì DELETE vật lý | FR-AUTH-03; §5.1 (Data retention) |

### 2.4 Indexes

| Index | Cột | Loại | Mục đích |
|---|---|---|---|
| `uq_active_user_phone` | `phone WHERE deleted_at IS NULL` | UNIQUE (Partial) | Đảm bảo mỗi số điện thoại chỉ thuộc về một user chưa bị xóa; cho phép tái sử dụng số điện thoại sau soft-delete |
| `uq_active_user_email_normalized` | `lower(email) WHERE deleted_at IS NULL AND email IS NOT NULL` | UNIQUE (Partial, Functional) | Case-insensitive uniqueness cho email; `lower()` tránh trường hợp `User@email.com` và `user@email.com` bị coi là khác nhau |

---

## 3. Bảng `accounts` — Tài khoản vận hành theo ngữ cảnh

### 3.1 Mục đích nghiệp vụ

Tách bạch **định danh con người** (`users`) khỏi **ngữ cảnh vận hành**. Một người (user) có thể có nhiều account gắn với các tenant/site khác nhau. Ví dụ: một cá nhân vừa là Driver (account không gắn tenant) vừa là Operator của Parking Lot A (account gắn `tenant_id` của A). Bảng này là đơn vị phân quyền — `account_roles` và `operator_grants` đều tham chiếu đến `accounts.id`, không phải `users.id`.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-02** | C | Authentication và authorization scope — "current account access state" được kiểm tra tại mỗi protected request |
| **FR-AUTH-04** | R | Operator provisioning, lot assignment: Owner tạo account Operator và gắn vào tenant/site |
| **FR-AUTH-06** | C | Với mỗi protected operation: kiểm tra `accounts.status` của account đang hành động |
| **§2.3.1** | — | Permission matrix: mọi permission đều có scope "Own account" / "Assigned lot" — scope này được biểu diễn qua `tenant_id`/`site_id` của account |
| **US-D08** | US | Driver tạo account cá nhân |
| **US-OW03** | US | Owner tạo và quản lý account Operator |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh account; đây là ID được dùng trong `account_roles`, `operator_grants`, `vehicles`, và là `account_id` được lưu dưới dạng Logical ID ở Reservation/Payment Service | Tự sinh bởi `uuid_generate_v4()` | FR-AUTH-02; FR-AUTH-06 |
| `user_id` | `UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE` | FK vật lý đến `users.id`; liên kết account với con người sở hữu nó. `CASCADE`: xóa user thì xóa luôn toàn bộ account của user đó | Gán bằng `users.id` của user đang đăng nhập/được tạo tài khoản | FR-AUTH-02 |
| `tenant_id` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `tenants.id` trong Parking Service DB. `NULL` với Driver account (không thuộc tenant nào). Khác `NULL` với Owner/Operator account | Được gán khi Owner đăng ký bãi hoặc Owner tạo Operator account; truyền qua API, không validate bằng FK vật lý | §2.3.1; FR-AUTH-04 |
| `site_id` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `parking_sites.id` trong Parking Service DB. Dùng để giới hạn scope của Operator vào một site cụ thể. `NULL` với Owner account (scope toàn tenant) | Được gán khi Owner assign Operator vào site cụ thể | §2.3.1 (OPERATOR_MANAGE scope) |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` CHECK IN (`'ACTIVE'`, `'INACTIVE'`, `'LOCKED'`, `'PENDING_APPROVAL'`) | Trạng thái vòng đời của account trong ngữ cảnh này. Hoạt động độc lập với `users.status` — một user ACTIVE có thể có account LOCKED tại một tenant cụ thể. FR-AUTH-06 yêu cầu kiểm tra **cả hai** | Mặc định `ACTIVE`; Owner/Admin có thể thay đổi | FR-AUTH-02; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo account | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: cho phép xóa logic account mà không mất dữ liệu lịch sử | Application gán = `NOW()` thay vì DELETE vật lý | §5.1 |

### 3.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_accounts_user_id` | `user_id` | Tra cứu nhanh toàn bộ account của một user |
| `idx_accounts_tenant_id` | `tenant_id` | Liệt kê toàn bộ account thuộc một tenant (dùng khi Owner/Admin quản lý) |
| `idx_accounts_site_id` | `site_id` | Liệt kê account Operator được gắn vào một site cụ thể |

### 3.5 Ghi chú Cross-service

`tenant_id` và `site_id` là **Logical IDs** — User Service **không** import hay validate chúng qua FK vật lý đến Parking Service DB. Tính nhất quán được đảm bảo bằng:
- Application validation: khi Owner/Admin gán `tenant_id`/`site_id`, User Service gọi Parking Service API để xác nhận sự tồn tại trước khi persist.
- Integration event: khi một tenant/site bị xóa bên Parking Service, event `TenantDeleted`/`SiteDeleted` có thể được phát để User Service soft-delete các account liên quan.

---

## 4. Bảng `roles` — Danh mục vai trò

### 4.1 Mục đích nghiệp vụ

Lưu định nghĩa chuẩn (master data) các vai trò trong hệ thống. Đây là bảng tĩnh, được seed dữ liệu ban đầu (`DRIVER`, `OWNER`, `OPERATOR`, `ADMIN`) và ít thay đổi. Tách vai trò thành bảng riêng cho phép mở rộng hệ thống phân quyền mà không cần sửa code.

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-03** | C | Chỉ role phù hợp mới được phép thực hiện profile-edit |
| **FR-AUTH-06** | C | Mỗi protected operation kiểm tra `role_code` qua `account_roles` để enforce permission |
| **§2.3** | — | Định nghĩa 4 actor: Driver, Operator, Owner, Admin → tương ứng 4 role codes |
| **§2.3.1** | — | Permission matrix xác định capability của từng role |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `code` | `VARCHAR(50) PK` | Mã định danh vai trò; dùng làm khóa chính để join với `account_roles`. Ví dụ: `'DRIVER'`, `'OPERATOR'`, `'OWNER'`, `'ADMIN'` | Seed cứng khi deploy; không do user tạo ra | FR-AUTH-06; §2.3 |
| `name` | `VARCHAR(100) NOT NULL` | Tên hiển thị của vai trò. Ví dụ: `'Tài xế'`, `'Nhân viên vận hành'`, `'Chủ bãi đỗ xe'`, `'Quản trị hệ thống'` | Seed cứng khi deploy | §2.3 |
| `description` | `TEXT NULL` | Mô tả chi tiết trách nhiệm và quyền hạn của vai trò; dùng cho mục đích tài liệu hóa | Seed cứng khi deploy | §2.3.1 (Permission Matrix) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi role | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |

---

## 5. Bảng `account_roles` — Gán vai trò cho tài khoản

### 5.1 Mục đích nghiệp vụ

Bảng junction thực hiện quan hệ nhiều-nhiều giữa `accounts` và `roles`. Một account có thể có nhiều role trong ngữ cảnh của mình. Đây là bảng được tra cứu tại **mỗi protected request** để xác định quyền của người dùng.

### 5.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-06** | C | Kiểm tra `account_roles` tại mỗi request để lấy danh sách role hiện tại của account |
| **FR-AUTH-04** | R | Owner gán role OPERATOR cho account mới tạo |
| **FR-AUTH-05** | C | Admin gán role OWNER sau khi approve `owner_applications` |
| **§2.3.1** | — | Role changes remain audited — `created_at` ghi nhận thời điểm gán role |

### 5.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `account_id` | `UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE` | FK đến account được gán role. `CASCADE`: xóa account thì xóa luôn toàn bộ role assignment của account đó | Gán bằng `accounts.id` tương ứng | FR-AUTH-06 |
| `role_code` | `VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT` | FK đến mã vai trò. `RESTRICT`: không cho xóa role nếu vẫn còn assignment; bảo vệ tính toàn vẹn của cấu hình phân quyền | Gán bằng `roles.code` tương ứng | FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm gán role cho account; phục vụ audit trail theo yêu cầu "role changes remain audited" | Tự sinh bởi database tại INSERT | §2.3.1; §4.3 (Audit log 7 năm) |

**Primary Key**: `(account_id, role_code)` — một account không thể có cùng một role hai lần.

### 5.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_account_roles_role_code` | `role_code` | Tra cứu ngược: tìm toàn bộ account đang giữ một role cụ thể (dùng khi Admin quản lý) |

---

## 6. Bảng `vehicles` — Phương tiện của Driver

### 6.1 Mục đích nghiệp vụ

Lưu trữ hồ sơ phương tiện đã đăng ký của Driver. Đây là nguồn thông tin phương tiện chính thống để:
- Nhận diện xe tại cổng ra/vào bằng LPR (khớp `normalized_plate`).
- Kiểm tra tính tương thích phương tiện với slot khi đặt chỗ.
- Liên kết phương tiện với reservation/session tại Reservation Service.

Lưu ý: Phương tiện không có biển số (xe không biển) **không** tạo bản ghi `vehicles` — chúng sử dụng ticket reference do Operator tạo trong Parking Session (FR-VEH-03).

### 6.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-VEH-01** | C | Driver có thể liệt kê và sửa phương tiện đã đăng ký; SmartPark lưu cả `original_plate` và `normalized_plate` |
| **FR-VEH-03** | C | Phương tiện không biển số dùng Operator-managed session/ticket, không tạo bản ghi vehicles |
| **FR-VEH-04** | C | Validate tính tương thích phương tiện-slot theo §3.4.3; `vehicle_type` là trường dữ liệu cốt lõi cho việc này |
| **§3.1.3** | — | Mỗi xe phải có license plate, vehicle type, vehicle image; validate format biển số Việt Nam; lưu cả raw và normalized value |
| **BR-VEH-01** | — | Normalized plated-vehicle storage: lưu cả original và canonical normalized plate |
| **§5.1** | — | Thông tin xe liên quan đến Thông tư 79/2024/TT-BCA (đăng ký và biển số xe) |
| **US-D09** | US | "Là Driver, tôi muốn đăng ký và quản lý thông tin phương tiện của mình" |
| **US-O01** | US | Operator sử dụng thông tin xe khi check-in (áp dụng FR-VEH-04) |

### 6.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của phương tiện. Được dùng làm Logical ID khi Reservation/IoT Service cần tham chiếu đến phương tiện cụ thể | Tự sinh bởi `uuid_generate_v4()` | FR-VEH-01 |
| `account_id` | `UUID NULL REFERENCES accounts(id) ON DELETE SET NULL` | FK đến account của Driver sở hữu xe. `NULL`: cho phép giữ lại bản ghi phương tiện ngay cả khi account bị xóa — bảo toàn lịch sử parking session. `SET NULL` thay vì CASCADE là quyết định quan trọng để duy trì audit trail | Gán bằng `accounts.id` của Driver khi đăng ký xe | FR-VEH-01; §3.1.3 |
| `original_plate` | `VARCHAR(20) NOT NULL` | Biển số xe **đúng như người dùng nhập**, bao gồm dấu cách, ký tự hoa/thường. Lưu trữ cho mục đích hiển thị và audit. Không dùng để matching | Do Driver nhập, được validate format theo Thông tư 79/2024/TT-BCA trước khi lưu | FR-VEH-01; §3.1.3; §5.1 |
| `normalized_plate` | `VARCHAR(20) NOT NULL` | Biển số xe **đã chuẩn hóa** (canonical form): loại bỏ dấu cách, chuyển hoa, chuẩn hóa format. Đây là giá trị dùng để **khớp với LPR** và kiểm tra trùng lặp (`EXCLUDE` constraint tại `reservations`, `monthly_passes`) | Sinh từ `original_plate` bởi lớp Application (normalization logic theo Thông tư 79/2024/TT-BCA) trước khi INSERT | FR-VEH-01; §3.1.3; BR-VEH-01 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` CHECK IN (`'CAR'`, `'MOTORCYCLE'`) | Loại phương tiện; quyết định tính tương thích với slot khi đặt chỗ. Motorcycle được hỗ trợ trong MVP. EV charging không phải function của hệ thống | Do Driver chọn khi đăng ký xe | FR-VEH-04; §3.1.3; FR-VEH-03 |
| `image_url` | `TEXT NULL` | URL ảnh phương tiện (mặt trước, thấy rõ biển số). Dùng cho mục đích nhận diện thủ công và LPR (§6.2) | URL do frontend upload lên storage (S3/CDN); `NULL` nếu chưa có ảnh | §3.1.3; §6.2 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm đăng ký phương tiện | Tự sinh bởi database | FR-VEH-01 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete phương tiện. Driver có thể "xóa" xe khỏi danh sách nhưng dữ liệu phải được giữ để tham chiếu từ lịch sử parking session | Application gán = `NOW()` thay vì DELETE vật lý | §5.1; FR-VEH-01 |

### 6.4 Indexes

| Index | Cột | Loại | Mục đích |
|---|---|---|---|
| `uq_active_vehicle_plate` | `normalized_plate WHERE deleted_at IS NULL` | UNIQUE (Partial) | Mỗi biển số chuẩn hóa chỉ được đăng ký bởi một chủ xe tại một thời điểm; cho phép tái sử dụng biển số sau soft-delete |
| `idx_vehicles_account_id` | `account_id` | INDEX | Tra cứu nhanh danh sách xe của một Driver |

### 6.5 Ghi chú Cross-service

`normalized_plate` là **khóa đối sánh** quan trọng nhất giữa User Service và các service khác:
- **Reservation Service** (`reservations.normalized_plate`, `monthly_passes.normalized_plate`): được cache tại bảng reservation để tránh query ngược về User Service tại mỗi EXCLUDE constraint check.
- **IoT Service** (`lpr_events`): LPR trả về plate text, Reservation Service dùng để tra cứu phương tiện.
- Cơ chế: User Service publish event `VehicleRegistered` / `VehiclePlateUpdated` khi plate thay đổi; các service liên quan consume để cập nhật cache.

---

## 7. Bảng `user_refresh_tokens` — Vòng đời Token xác thực

### 7.1 Mục đích nghiệp vụ

Quản lý vòng đời của cặp token JWT (Access Token + Refresh Token) theo yêu cầu §3.1.4:
- Access Token: hết hạn sau **24 giờ**.
- Refresh Token: hết hạn sau **7 ngày**.
- Hỗ trợ revocation: khi logout, `is_revoked = true` để invalidate token ngay lập tức mà không cần đợi hết hạn.
- Liên kết device/IP để phát hiện đăng nhập bất thường.

### 7.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-02** | C | Logout phải terminate session/renewal; revoked token không được authorize subsequent request |
| **FR-AUTH-06** | C | Token hợp lệ không đủ nếu account bị lock — cần kiểm tra `is_revoked` + `users.status` + `accounts.status` |
| **§3.1.4** | — | Session token 24h, refresh 7 ngày; MFA TOTP optional |
| **§4.3** | NFR | JWT RS256; token expiry values |

### 7.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của bản ghi token | Tự sinh bởi `uuid_generate_v4()` | — |
| `user_id` | `UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE` | FK đến user sở hữu token. `CASCADE`: xóa user thì xóa toàn bộ token — tránh orphan token | Gán bằng `users.id` của user đang đăng nhập | FR-AUTH-02 |
| `token_hash` | `VARCHAR(255) NOT NULL UNIQUE` | Giá trị băm của Refresh Token (plain-text Refresh Token **không** được lưu). Khi client gửi refresh request, server băm token nhận được và so sánh với cột này | Sinh bởi Application: `SHA-256(raw_refresh_token)`; UNIQUE enforce một token chỉ tồn tại một lần | §4.3 (Security) |
| `device_info` | `VARCHAR(255) NULL` | Thông tin thiết bị (user-agent string, tên thiết bị). Dùng để hiển thị danh sách session cho người dùng và phát hiện đăng nhập bất thường | Trích xuất từ HTTP `User-Agent` header tại thời điểm login | §4.3 (Security monitoring) |
| `ip_address` | `VARCHAR(45) NULL` | IP của client tại thời điểm đăng nhập. `VARCHAR(45)` đủ để lưu cả IPv4 và IPv6 | Trích xuất từ request tại API Gateway / Application | §4.3 |
| `expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm Refresh Token hết hạn. Theo §3.1.4 = `created_at + 7 days` | Tính bởi Application = `NOW() + INTERVAL '7 days'` | §3.1.4; §4.3 |
| `access_token_id` | `UUID NOT NULL UNIQUE` | Định danh duy nhất của Access Token tương ứng (JWT claim `jti`). Dùng để revoke Access Token cụ thể mà không revoke toàn bộ session | Sinh bởi Application khi tạo JWT; gán vào JWT claim `jti` | FR-AUTH-02; §3.1.4 |
| `access_expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm Access Token hết hạn. Theo §3.1.4 = `created_at + 24 hours` | Tính bởi Application = `NOW() + INTERVAL '24 hours'` | §3.1.4; §4.3 |
| `is_revoked` | `BOOLEAN NOT NULL DEFAULT false` | Cờ revocation: `true` khi token bị vô hiệu hóa trước hạn (logout, đổi mật khẩu, Admin lock). Server kiểm tra cờ này trước khi accept refresh request | Application set = `true` tại logout hoặc force-revoke | FR-AUTH-02; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cấp token (login time) | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật (thường là khi revoke) | Application cập nhật khi `is_revoked` thay đổi | — |

**Constraint**: `CONSTRAINT chk_expires_at_valid CHECK (expires_at > created_at)` — đảm bảo token không bao giờ được tạo với `expires_at` trong quá khứ.

### 7.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_user_refresh_tokens_user_id` | `user_id` | Tra cứu toàn bộ token của một user (dùng khi logout tất cả thiết bị) |
| `idx_user_refresh_tokens_expires_at` | `expires_at` | Hỗ trợ cleanup job: xóa hàng loạt token đã hết hạn |

---

## 8. Bảng `driver_registrations` — Đăng ký OTP cho Driver

### 8.1 Mục đích nghiệp vụ

Quản lý luồng xác minh OTP trong quá trình đăng ký tài khoản Driver. Bảng này lưu trạng thái của OTP hiện tại, bao gồm: mã băm, kênh gửi, thời hạn, số lần thất bại, và khóa chống gửi lại quá nhanh. Một user chỉ có **một** bản ghi đăng ký tại một thời điểm (`UNIQUE user_id`).

### 8.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-01** | C | Đăng ký bằng phone hoặc email → gửi OTP → xác minh theo §3.1.1 |
| **§3.1.1** | — | OTP valid 5 phút; sau 3 lần nhập sai → khóa 15 phút; hỗ trợ resend |
| **§4.3** | NFR | OTP Expiry 5 phút; Account Lockout 15 phút/3 lần thất bại |
| **US-D08** | US | Driver đăng ký tài khoản mới |

### 8.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi đăng ký | Tự sinh bởi `uuid_generate_v4()` | — |
| `user_id` | `UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE` | FK đến user đang trong quá trình đăng ký. `UNIQUE`: một user chỉ có thể có một luồng OTP đang hoạt động. `CASCADE`: xóa user thì xóa bản ghi đăng ký | Gán bằng `users.id` khi tạo user mới | FR-AUTH-01 |
| `channel` | `VARCHAR(10) NOT NULL` CHECK IN (`'email'`, `'sms'`) | Kênh gửi OTP: `'email'` hoặc `'sms'`. Xác định cách Notification Service gửi OTP và cách user xác minh | Do user chọn (hoặc hệ thống tự chọn dựa trên thông tin đăng ký: có email dùng email, có phone dùng sms) | FR-AUTH-01; §3.1.1 |
| `code_hash` | `TEXT NOT NULL` | Giá trị băm của OTP (plain-text OTP **không** được lưu). Khi user nhập OTP, server băm và so sánh | Sinh bởi Application: tạo OTP ngẫu nhiên (6 chữ số) → băm bằng bcrypt/SHA-256 → lưu hash; OTP gốc gửi qua Notification Service | FR-AUTH-01; §4.3 |
| `expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm OTP hết hạn. Theo §3.1.1 và §4.3 = `created_at + 5 minutes` | Tính bởi Application = `NOW() + INTERVAL '5 minutes'` | §3.1.1; §4.3 |
| `resend_available_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm user được phép yêu cầu gửi lại OTP. Tránh spam resend request | Tính bởi Application = `NOW() + INTERVAL '1-2 minutes'` (configurable); phải nhỏ hơn `expires_at` | §3.1.1 |
| `failed_attempts` | `INTEGER NOT NULL DEFAULT 0` CHECK (`failed_attempts BETWEEN 0 AND 3`) | Số lần nhập OTP sai liên tiếp. Giới hạn tối đa 3 lần (theo §3.1.1 và §4.3). Constraint DB đảm bảo không vượt quá 3 | Tăng 1 mỗi lần user nhập OTP sai | §3.1.1; §4.3 |
| `locked_until` | `TIMESTAMPTZ NULL` | Thời điểm kết thúc khóa OTP sau khi đạt 3 lần thất bại. `NULL` = chưa bị khóa | Gán = `NOW() + INTERVAL '15 minutes'` khi `failed_attempts` đạt 3 (§4.3) | §3.1.1; §4.3 |
| `verified_at` | `TIMESTAMPTZ NULL` | Thời điểm OTP được xác minh thành công. `NULL` = chưa xác minh. Khi có giá trị → user đã hoàn tất đăng ký | Gán = `NOW()` khi OTP khớp và còn hạn | FR-AUTH-01 |

---

## 9. Bảng `owner_applications` — Đơn đăng ký Chủ bãi xe

### 9.1 Mục đích nghiệp vụ

Quản lý quy trình duyệt đơn đăng ký làm Owner. Theo §3.1.1 và FR-AUTH-05, Owner registration phải có Admin approval trước khi tài khoản được kích hoạt cho các hoạt động Owner. Bảng này lưu toàn bộ thông tin đơn và kết quả duyệt.

### 9.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-05** | C | Admin duyệt Owner registration sau khi xác nhận đủ company name, email, phone; role changes remain audited |
| **§3.1.1** | — | Owner registration yêu cầu company name, email, phone; phải qua Admin approval trước khi active |
| **§2.3.1** | — | `ACCOUNT_ADMIN`: Admin approves Owner registration |
| **US-A01** | US | "Là Admin, tôi muốn phê duyệt hoặc từ chối đơn đăng ký chủ bãi xe" |

### 9.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh đơn đăng ký | Tự sinh bởi `uuid_generate_v4()` | — |
| `user_id` | `UUID NOT NULL UNIQUE REFERENCES users(id)` | FK đến user nộp đơn. `UNIQUE`: mỗi user chỉ có thể có một đơn đăng ký (không cho nộp lại khi đã có đơn pending/rejected) | Gán bằng `users.id` của người nộp đơn | FR-AUTH-05 |
| `business_name` | `VARCHAR(255) NOT NULL` | Tên doanh nghiệp/công ty sở hữu bãi đỗ xe. Đây là thông tin Admin kiểm tra khi duyệt (§3.1.1 yêu cầu company name) | Do user nhập khi nộp đơn; phải trùng hoặc liên kết với `users.company_name` | FR-AUTH-05; §3.1.1 |
| `lot_type` | `VARCHAR(30) NOT NULL` CHECK IN (`'outdoor'`, `'basement'`, `'multi-storey'`) | Loại bãi đỗ xe dự định vận hành. Dùng để Admin đánh giá quy mô và yêu cầu kỹ thuật trước khi duyệt | Do user chọn khi nộp đơn | FR-AUTH-05 |
| `status` | `VARCHAR(20) NOT NULL DEFAULT 'pending'` CHECK IN (`'pending'`, `'approved'`, `'rejected'`) | Trạng thái xử lý đơn. `pending`: chờ Admin xem xét. `approved`: được chấp thuận → system kích hoạt role OWNER cho account. `rejected`: bị từ chối | `'pending'` khi tạo; Admin cập nhật thành `'approved'` hoặc `'rejected'` | FR-AUTH-05 |
| `submitted_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm nộp đơn chính thức. Có thể khác `created_at` nếu hệ thống cho phép lưu nháp | Do Application gán = `NOW()` khi user submit | FR-AUTH-05 |
| `reviewed_at` | `TIMESTAMPTZ NULL` | Thời điểm Admin xem xét và ra quyết định. `NULL` khi `status = 'pending'` | Do Application gán = `NOW()` khi Admin cập nhật `status` | FR-AUTH-05 |
| `reviewed_by` | `UUID NULL REFERENCES users(id)` | FK đến `users.id` của Admin đã xem xét đơn. `NULL` khi `status = 'pending'`. Phục vụ audit trail ("role changes remain audited") | Gán bằng `users.id` của Admin đang thực hiện review | FR-AUTH-05; §2.3.1 |
| `review_note` | `VARCHAR(2000) NULL` | Ghi chú của Admin khi duyệt hoặc từ chối. Bắt buộc điền khi `status = 'rejected'` để thông báo lý do cho Owner | Do Admin nhập khi review | FR-AUTH-05 |

**Constraint**: `CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL) OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))` — bảo đảm tính nhất quán: đơn đang xử lý thì chưa có người duyệt, đơn đã xử lý thì phải có người duyệt.

---

## 10. Bảng `operator_grants` — Phân quyền vận hành bãi

### 10.1 Mục đích nghiệp vụ

Lưu trữ **bộ quyền vận hành chi tiết** được cấp cho một Operator account tại một site cụ thể. Không phải mọi Operator đều có tất cả quyền — Owner chỉ cấp những quyền cần thiết cho từng nhân viên (least-privilege). Đây là extension của RBAC chuẩn: role `OPERATOR` xác định ngữ cảnh, `operator_grants.permissions` xác định capability cụ thể.

### 10.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-04** | R | Operator provisioning và permission management: Owner gán permissions cho Operator theo §3.7.5 |
| **FR-AUTH-06** | C | Protected operations (CASH_COLLECT, EMERGENCY_GATE_RELEASE, APPEAL_REVIEW, v.v.) kiểm tra `operator_grants.permissions` |
| **§2.3.1** | — | Permission matrix: DEVICE_MANAGE, DEVICE_STATUS_VIEW, CASH_COLLECT, APPEAL_REVIEW, SLOT_OVERRIDE, VIOLATION_REVIEW, EMERGENCY_GATE_RELEASE đều là "Explicit lot grant" |
| **BR-AUTH-01** | — | Scoped roles — delegated, lot-scoped permissions |
| **US-OW03** | US | "Là Owner, tôi muốn quản lý nhân viên vận hành và phân quyền cho họ" |

### 10.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `account_id` | `UUID PK REFERENCES accounts(id) ON DELETE CASCADE` | FK đến account Operator được cấp quyền. Dùng làm Primary Key vì mỗi account chỉ có một bộ `operator_grants` (1-1 relationship). `CASCADE`: xóa account thì xóa grants | Gán bằng `accounts.id` của Operator account | FR-AUTH-04; §2.3.1 |
| `created_by` | `UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT` | FK đến `users.id` của Owner đã tạo grant. `RESTRICT`: không cho xóa user nếu vẫn còn grant do user đó tạo — bảo đảm audit trail không mất thông tin người cấp phép | Gán bằng `users.id` của Owner đang thực hiện cấp quyền | FR-AUTH-04; §2.3.1 (audit) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cấp quyền; phục vụ audit trail | Tự sinh bởi database | §2.3.1; §4.3 |
| `permissions` | `TEXT[] NOT NULL` với 3 constraints | Mảng các quyền vận hành được cấp cho Operator tại site. Danh sách quyền hợp lệ bị giới hạn cứng bởi database constraint. Xem chi tiết bên dưới | Owner chọn từ danh sách quyền hợp lệ khi tạo/cập nhật grant | FR-AUTH-04; FR-AUTH-06; §2.3.1 |

**Các constraints trên `permissions`:**

| Constraint | SQL | Ý nghĩa |
|---|---|---|
| Số lượng phần tử | `CHECK (cardinality(permissions) BETWEEN 1 AND 4)` | Phải cấp ít nhất 1 quyền, tối đa 4 quyền (bằng tổng số quyền hợp lệ) |
| Không có NULL trong mảng | `CHECK (array_position(permissions, NULL) IS NULL)` | Không cho phép giá trị NULL trong mảng permissions |
| Giá trị hợp lệ | `CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW']::TEXT[])` | Chỉ chấp nhận 4 quyền này; đây là tập con của toàn bộ "Explicit lot grant" trong §2.3.1 |

**Danh sách quyền và ý nghĩa:**

| Quyền | Ý nghĩa nghiệp vụ | Mapping SRS |
|---|---|---|
| `DEVICE_MANAGE` | Operator được phép quản lý thiết bị (camera, cổng, sensor) tại lot được phân công | §2.3.1 |
| `DEVICE_STATUS_VIEW` | Operator được phép xem trạng thái thiết bị tại lot được phân công | §2.3.1 |
| `CASH_COLLECT` | Operator được phép ghi nhận thu tiền mặt qua CASH_COLLECT interface | §2.3.1 |
| `APPEAL_REVIEW` | Operator được phép xem xét và phê duyệt khiếu nại của Driver | §2.3.1 |

> **Lưu ý**: SLOT_OVERRIDE, VIOLATION_REVIEW và EMERGENCY_GATE_RELEASE cũng là "Explicit lot grant" trong §2.3.1 nhưng **chưa có trong `permissions` array**. Đây có thể là phạm vi MVP v0.9 — các quyền này có thể được bổ sung trong phiên bản sau.

### 10.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_operator_grants_creator` | `created_by` | Tra cứu tất cả grants do một Owner cụ thể tạo ra; dùng cho audit và khi Owner bị xóa |

---

## 11. Tóm tắt quan hệ giữa các bảng

```
users (1) ──── (N) accounts
users (1) ──── (0..1) driver_registrations
users (1) ──── (0..1) owner_applications
users (1) ──── (N) user_refresh_tokens

accounts (1) ──── (N) account_roles
accounts (N) ──── (N) roles  [qua account_roles]
accounts (1) ──── (N) vehicles  [account_id trong vehicles]
accounts (1) ──── (0..1) operator_grants

operator_grants.created_by ──── users.id  [audit FK]
owner_applications.reviewed_by ──── users.id [audit FK]
```

---

## 12. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `tenant_id` | `accounts` | Parking Service (`tenants.id`) | Application validates qua Parking Service API trước khi persist; event `TenantDeleted` để cleanup |
| `site_id` | `accounts` | Parking Service (`parking_sites.id`) | Application validates qua Parking Service API trước khi persist; event `SiteDeleted` để cleanup |
| `normalized_plate` | `vehicles` | Reservation Service, IoT Service | Kafka event `VehicleRegistered` / `VehiclePlateUpdated`; Reservation Service cache plate |
| `account_id` | `vehicles` | Reservation Service, Payment Service | Logical ID; consumer services gọi User Service API để resolve thông tin đầy đủ khi cần |

---

## 13. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key |
