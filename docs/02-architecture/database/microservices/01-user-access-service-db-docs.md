# User & Access Service — Database Documentation

> **Service**: User & Access Service (`UserDb`)  
> **Database file**: [`01-user-access-service-db.sql`](../../../../scripts/database/microservices/01-user-access-service-db.sql)  
> **SRS baseline**: v1.0 RC3 (Commit `6165024`, replacing v0.9)  
> **Message Broker**: RabbitMQ + MassTransit  
> **Scope**: Quản lý định danh con người (`users`), tổ chức/doanh nghiệp (`tenants`), tài khoản theo ngữ cảnh (`accounts`), phân quyền RBAC (`roles`, `account_roles`), phương tiện (`vehicles`), vòng đời token xác thực (`user_refresh_tokens`), đăng ký OTP tài xế (`driver_registrations`), đơn đăng ký chủ bãi (`owner_applications`), phân quyền vận hành bãi (`operator_grants`), xác thực nâng cao (`auth_challenges`, `workflow_deliveries`), và MFA/TOTP (`mfa_credentials`).

---

## 1. Tổng quan kiến trúc

User & Access Service là **dịch vụ định danh, bảo mật và phân quyền trung tâm** của hệ thống SmartPark theo chuẩn kiến trúc v1.0 RC3. Service này quản lý cả danh tính người dùng cá nhân lẫn tổ chức pháp nhân (`tenants`). Toàn bộ các service khác (Facility, Operations, Payment, IoT, Notification) đều tham chiếu đến `user_id`, `account_id`, và `tenant_id` dưới dạng **Logical ID** — không có FK vật lý xuyên database.

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
| `email_verified_at` | `TIMESTAMPTZ NULL` | Thời điểm email được xác thực thành công qua mã OTP | Application gán = `NOW()` khi xác thực thành công | FR-AUTH-03 |
| `phone_verified_at` | `TIMESTAMPTZ NULL` | Thời điểm số điện thoại được xác thực thành công qua mã OTP | Application gán = `NOW()` khi xác thực thành công | FR-AUTH-03 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái vòng đời của tài khoản người dùng. `PENDING_VERIFICATION`: Chờ xác thực OTP. `REJECTED`: Bị từ chối duyệt | Mặc định `ACTIVE` khi tạo. Chỉ Admin được thay đổi `status`, không phải customer | FR-AUTH-02; FR-AUTH-05; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi. Dùng cho audit và báo cáo | Tự sinh bởi database tại thời điểm INSERT | §4.3 (Audit); FR-AUTH-05 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật bản ghi lần cuối. Cần được cập nhật thủ công bởi Application mỗi khi có UPDATE | Application cập nhật = `NOW()` tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | **Soft-delete**: thời điểm xóa logic. `NULL` = chưa xóa; có giá trị = đã xóa. Cho phép giữ lại lịch sử và toàn vẹn dữ liệu tham chiếu | Application gán = `NOW()` thay vì DELETE vật lý | FR-AUTH-03; §5.1 (Data retention) |

### 2.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `chk_users_status` | `CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL', 'PENDING_VERIFICATION', 'REJECTED'))` | Giới hạn trạng thái tài khoản hợp lệ |
| `uq_active_user_phone` | `UNIQUE(phone) WHERE deleted_at IS NULL` | Đảm bảo mỗi số điện thoại chỉ thuộc về một user chưa bị xóa; cho phép tái sử dụng số điện thoại sau soft-delete |
| `uq_active_user_email_normalized` | `UNIQUE(lower(email)) WHERE deleted_at IS NULL AND email IS NOT NULL` | Case-insensitive uniqueness cho email; `lower()` tránh trường hợp `User@email.com` và `user@email.com` bị coi là khác nhau |

---

---

## 3. Bảng `tenants` — Doanh nghiệp / Tổ chức bãi xe

### 3.1 Mục đích nghiệp vụ

Quản lý thông tin **pháp nhân doanh nghiệp hoặc đơn vị vận hành bãi xe**. Theo kiến trúc chuẩn hóa v1.0 RC3, thực thể `tenants` thuộc quyền sở hữu chính thức của User & Access Service:
- Lưu trữ thông tin pháp lý, mã doanh nghiệp, mã số thuế và thông tin liên hệ.
- Là đơn vị tổ chức cha sở hữu các tài khoản Owner/Operator (`accounts.tenant_id`).
- Được liên kết với đơn đăng ký bãi xe `owner_applications`.
- Các service khác (Facility, Operations, Payment) chỉ lưu `tenant_id` dưới dạng **Logical Reference**.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-05** | C | Phê duyệt đăng ký doanh nghiệp chủ bãi |
| **C-23** | — | Thông tin pháp nhân: business name, contact info |
| **BR-AUTH-01** | — | Scope tổ chức và phân quyền đa người thuê (Multi-tenancy) |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của tổ chức/tenant | Tự sinh bởi `uuid_generate_v4()` | FR-AUTH-05 |
| `code` | `VARCHAR(50) NOT NULL` | Mã định danh doanh nghiệp (viết hoa, không dấu, duy nhất) | Do Admin/Owner thiết lập khi duyệt đơn | C-23 |
| `name` | `VARCHAR(255) NOT NULL` | Tên pháp nhân công ty / doanh nghiệp | Lấy từ `owner_applications.business_name` | FR-AUTH-05 |
| `contact_email` | `VARCHAR(255) NOT NULL` | Email liên hệ chính thức của tổ chức | Do Owner cung cấp lúc đăng ký | FR-AUTH-05 |
| `contact_phone` | `VARCHAR(20) NOT NULL` | Số điện thoại đại diện doanh nghiệp | Do Owner cung cấp lúc đăng ký | FR-AUTH-05 |
| `tax_code` | `VARCHAR(50) NULL` | Mã số thuế của doanh nghiệp | Do Owner cung cấp | C-23 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái hoạt động: `ACTIVE`, `INACTIVE`, `SUSPENDED` | Quản lý bởi Admin | BR-AUTH-01 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi | Database tự sinh | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật cuối | Application cập nhật | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete timestamp | Application cập nhật | — |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_active_tenant_code` | `UNIQUE(code) WHERE deleted_at IS NULL` | Đảm bảo mã tenant duy nhất cho các tổ chức đang hoạt động |
| `idx_tenants_status` | `status` | Tối ưu lọc tenant theo trạng thái |

## 4. Bảng `accounts` — Tài khoản vận hành theo ngữ cảnh

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
| `user_id` | `UUID NOT NULL` | FK vật lý đến `users.id`; liên kết account với con người sở hữu nó | Gán bằng `users.id` của user đang đăng nhập/được tạo tài khoản | FR-AUTH-02 |
| `tenant_id` | `UUID NULL` | **FK nội bộ** trỏ đến `tenants.id` trong UserDb (`ON DELETE SET NULL`). `NULL` với Driver account (không thuộc tenant nào). Khác `NULL` với Owner/Operator account | Gán khi duyệt Owner application hoặc khi Owner tạo Operator account; đảm bảo tính toàn vẹn tổ chức | §2.3.1; FR-AUTH-04 |
| `site_id` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `parking_sites.id` trong Parking Service DB. Dùng để giới hạn scope của Operator vào một site cụ thể. `NULL` với Owner account (scope toàn tenant) | Được gán khi Owner assign Operator vào site cụ thể | §2.3.1 (OPERATOR_MANAGE scope) |
| `permissions` | `TEXT[] NULL` | Danh sách quyền hạn bổ sung hoặc bị ghi đè của tài khoản | Cập nhật bởi Admin/Owner khi gán quyền | FR-AUTH-06 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái vòng đời của account trong ngữ cảnh này. Hoạt động độc lập với `users.status` — một user ACTIVE có thể có account LOCKED tại một tenant cụ thể. FR-AUTH-06 yêu cầu kiểm tra **cả hai** | Mặc định `ACTIVE`; Owner/Admin có thể thay đổi | FR-AUTH-02; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo account | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: cho phép xóa logic account mà không mất dữ liệu lịch sử | Application gán = `NOW()` thay vì DELETE vật lý | §5.1 |

### 3.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_accounts_users` | `REFERENCES users(id) ON DELETE CASCADE` | Xóa user thì xóa luôn toàn bộ account của user đó |
| `chk_accounts_status` | `CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED', 'PENDING_APPROVAL'))` | Giới hạn trạng thái account hợp lệ |
| `idx_accounts_user_id` | `user_id` | Tra cứu nhanh toàn bộ account của một user |
| `idx_accounts_tenant_id` | `tenant_id` | Liệt kê toàn bộ account thuộc một tenant (dùng khi Owner/Admin quản lý) |
| `idx_accounts_site_id` | `site_id` | Liệt kê account Operator được gắn vào một site cụ thể |

### 4.5 Ghi chú Quan hệ & Cross-service

- `tenant_id` là **FK nội bộ** tham chiếu tới `tenants.id` ngay trong `UserDb` (`ON DELETE SET NULL`).
- `site_id` là **Logical ID** tham chiếu tới `parking_sites.id` trong Facility & Visualization Service (`FacilityDb`). User & Access Service không enforce FK vật lý xuyên database.
- Tính nhất quán với `site_id` được đảm bảo bằng API validation và RabbitMQ integration events (`SiteCreated`, `SiteDeleted`).

---

## 5. Bảng `roles` — Danh mục vai trò

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

## 6. Bảng `account_roles` — Gán vai trò cho tài khoản

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
| `account_id` | `UUID NOT NULL` | FK đến account được gán role | Gán bằng `accounts.id` tương ứng | FR-AUTH-06 |
| `role_code` | `VARCHAR(50) NOT NULL` | FK đến mã vai trò | Gán bằng `roles.code` tương ứng | FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm gán role cho account; phục vụ audit trail theo yêu cầu "role changes remain audited" | Tự sinh bởi database tại INSERT | §2.3.1; §4.3 (Audit log 7 năm) |

**Primary Key**: `(account_id, role_code)` — một account không thể có cùng một role hai lần.

### 5.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_account_roles_accounts` | `REFERENCES accounts(id) ON DELETE CASCADE` | Xóa account thì xóa luôn toàn bộ role assignment của account đó |
| `FK_account_roles_roles` | `REFERENCES roles(code) ON DELETE RESTRICT` | Không cho xóa role nếu vẫn còn assignment; bảo vệ tính toàn vẹn của cấu hình phân quyền |
| `idx_account_roles_role_code` | `role_code` | Tra cứu ngược: tìm toàn bộ account đang giữ một role cụ thể (dùng khi Admin quản lý) |

---

## 7. Bảng `vehicles` — Phương tiện của Driver

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
| `account_id` | `UUID NULL` | FK đến account của Driver sở hữu xe. `NULL`: cho phép giữ lại bản ghi phương tiện ngay cả khi account bị xóa — bảo toàn lịch sử parking session | Gán bằng `accounts.id` của Driver khi đăng ký xe | FR-VEH-01; §3.1.3 |
| `original_plate` | `VARCHAR(30) NOT NULL` | Biển số xe **đúng như người dùng nhập**, bao gồm dấu cách, ký tự hoa/thường. Lưu trữ cho mục đích hiển thị và audit. Không dùng để matching | Do Driver nhập, được validate format theo Thông tư 79/2024/TT-BCA trước khi lưu | FR-VEH-01; §3.1.3; §5.1 |
| `normalized_plate` | `VARCHAR(20) NOT NULL` | Biển số xe **đã chuẩn hóa** (canonical form): loại bỏ dấu cách, chuyển hoa, chuẩn hóa format. Đây là giá trị dùng để **khớp với LPR** và kiểm tra trùng lặp (`EXCLUDE` constraint tại `reservations`, `monthly_passes`) | Sinh từ `original_plate` bởi lớp Application (normalization logic theo Thông tư 79/2024/TT-BCA) trước khi INSERT | FR-VEH-01; §3.1.3; BR-VEH-01 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại phương tiện; quyết định tính tương thích với slot khi đặt chỗ. Motorcycle được hỗ trợ trong MVP. EV charging không phải function của hệ thống | Do Driver chọn khi đăng ký xe | FR-VEH-04; §3.1.3; FR-VEH-03 |
| `image_url` | `TEXT NULL` | URL ảnh phương tiện (mặt trước, thấy rõ biển số). Dùng cho mục đích nhận diện thủ công và LPR (§6.2) | URL do frontend upload lên storage (S3/CDN); `NULL` nếu chưa có ảnh | §3.1.3; §6.2 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm đăng ký phương tiện | Tự sinh bởi database | FR-VEH-01 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete phương tiện. Driver có thể "xóa" xe khỏi danh sách nhưng dữ liệu phải được giữ để tham chiếu từ lịch sử parking session | Application gán = `NOW()` thay vì DELETE vật lý | §5.1; FR-VEH-01 |

### 6.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_vehicles_accounts` | `REFERENCES accounts(id) ON DELETE SET NULL` | `SET NULL` thay vì CASCADE là quyết định quan trọng để duy trì audit trail |
| `chk_vehicles_type` | `CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED'))` | Ràng buộc loại phương tiện hợp lệ chuẩn v1.0 |
| `uq_active_vehicle_plate` | `UNIQUE(normalized_plate) WHERE deleted_at IS NULL` | Đảm bảo mỗi biển số xe đã chuẩn hóa chỉ được kích hoạt bởi 1 tài khoản chưa bị xóa tại một thời điểm |
| `idx_vehicles_account_id` | `account_id` | Tra cứu nhanh danh sách xe của một Driver |

### 6.5 Ghi chú Cross-service

`normalized_plate` là **khóa đối sánh** quan trọng nhất giữa User Service và các service khác:
- **Reservation Service** (`reservations.normalized_plate`, `monthly_passes.normalized_plate`): được cache tại bảng reservation để tránh query ngược về User Service tại mỗi EXCLUDE constraint check.
- **IoT Service** (`lpr_events`): LPR trả về plate text, Reservation Service dùng để tra cứu phương tiện.
- Cơ chế: User Service publish event `VehicleRegistered` / `VehiclePlateUpdated` khi plate thay đổi; các service liên quan consume để cập nhật cache.

---

## 8. Bảng `user_refresh_tokens` — Vòng đời Token xác thực

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
| `user_id` | `UUID NOT NULL` | FK đến user sở hữu token. User đăng xuất/bị xóa → xóa token | Gán bằng `users.id` của user đang đăng nhập | FR-AUTH-02 |
| `token_hash` | `VARCHAR(255) NOT NULL` | Giá trị băm của Refresh Token (plain-text Refresh Token **không** được lưu) | Sinh bởi Application: `SHA-256(raw_refresh_token)` | §4.3 (Security) |
| `device_info` | `VARCHAR(255) NULL` | Thông tin thiết bị (user-agent string, tên thiết bị). Dùng để hiển thị danh sách session | Trích xuất từ HTTP `User-Agent` header | §4.3 (Security monitoring) |
| `ip_address` | `VARCHAR(45) NULL` | IP của client tại thời điểm đăng nhập | Trích xuất từ request tại API Gateway / Application | §4.3 |
| `expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm Refresh Token hết hạn | Tính bởi Application = `NOW() + INTERVAL '7 days'` | §3.1.4; §4.3 |
| `access_token_id` | `UUID NOT NULL` | Định danh duy nhất của Access Token tương ứng (JWT claim `jti`) | Sinh bởi Application khi tạo JWT; gán vào JWT claim `jti` | FR-AUTH-02; §3.1.4 |
| `access_expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm Access Token hết hạn | Tính bởi Application = `NOW() + INTERVAL '24 hours'` | §3.1.4; §4.3 |
| `is_revoked` | `BOOLEAN NOT NULL DEFAULT false` | Cờ revocation: `true` khi token bị vô hiệu hóa trước hạn | Application set = `true` tại logout hoặc force-revoke | FR-AUTH-02; FR-AUTH-06 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cấp token (login time) | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật (thường là khi revoke) | Application cập nhật khi `is_revoked` thay đổi | — |

### 7.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_user_refresh_tokens_users` | `REFERENCES users(id) ON DELETE CASCADE` | Xóa user thì xóa toàn bộ token |
| `uq_user_refresh_tokens_hash` | `UNIQUE(token_hash)` | Unique token hash |
| `uq_user_refresh_tokens_access` | `UNIQUE(access_token_id)` | Unique access token ID |
| `chk_expires_at_valid` | `CHECK (expires_at > created_at)` | Đảm bảo token không bao giờ được tạo với `expires_at` trong quá khứ |
| `idx_user_refresh_tokens_user_id` | `user_id` | Tra cứu toàn bộ token của một user (dùng khi logout tất cả thiết bị) |
| `idx_user_refresh_tokens_expires_at` | `expires_at` | Hỗ trợ cleanup job: xóa hàng loạt token đã hết hạn |

---

## 9. Bảng `driver_registrations` — Đăng ký OTP cho Driver

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
| `user_id` | `UUID NOT NULL` | FK đến user đang trong quá trình đăng ký | Gán bằng `users.id` khi tạo user mới | FR-AUTH-01 |
| `channel` | `VARCHAR(10) NOT NULL` | Kênh gửi OTP: `'email'` hoặc `'sms'` | Do user chọn | FR-AUTH-01; §3.1.1 |
| `code_hash` | `TEXT NOT NULL` | Giá trị băm của OTP | Sinh bởi Application | FR-AUTH-01; §4.3 |
| `expires_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm OTP hết hạn | Tính bởi Application = `NOW() + INTERVAL '5 minutes'` | §3.1.1; §4.3 |
| `resend_available_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm user được phép yêu cầu gửi lại OTP | Tính bởi Application = `NOW() + INTERVAL '1-2 minutes'` | §3.1.1 |
| `failed_attempts` | `INTEGER NOT NULL DEFAULT 0` | Số lần nhập OTP sai liên tiếp | Tăng 1 mỗi lần user nhập OTP sai | §3.1.1; §4.3 |
| `locked_until` | `TIMESTAMPTZ NULL` | Thời điểm kết thúc khóa OTP sau khi đạt 3 lần thất bại | Gán = `NOW() + INTERVAL '15 minutes'` khi `failed_attempts` đạt 3 | §3.1.1; §4.3 |
| `verified_at` | `TIMESTAMPTZ NULL` | Thời điểm OTP được xác minh thành công | Gán = `NOW()` khi OTP khớp và còn hạn | FR-AUTH-01 |

### 8.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_driver_registrations_users` | `REFERENCES users(id) ON DELETE CASCADE` | Xóa user thì xóa bản ghi đăng ký |
| `uq_driver_registrations_user` | `UNIQUE(user_id)` | Một user chỉ có thể có một luồng OTP đang hoạt động |
| `chk_driver_channel` | `CHECK (channel IN ('email', 'sms'))` | Giới hạn kênh gửi OTP |
| `chk_driver_failed_attempts` | `CHECK (failed_attempts BETWEEN 0 AND 3)` | Giới hạn số lần nhập sai tối đa 3 lần |

---

## 9A. Bảng `system_terms_and_conditions` — Điều khoản và thỏa thuận dịch vụ

### 9A.1 Mục đích nghiệp vụ

Quản lý các phiên bản điều khoản dịch vụ và thỏa thuận pháp lý của hệ thống SmartPark (như chính sách đăng ký Owner, quy định mở Site mới). Bảng này lưu URL tài liệu (PDF/Doc) trên Cloud Storage và trạng thái hiệu lực (`is_active`), bảo đảm tính pháp lý và cơ chế versioning khi các điều khoản thay đổi theo thời gian.

### 9A.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **System Governance** | Direct | Cơ chế quản lý điều khoản và sự đồng thuận pháp lý khi onboard Owner và tạo Site mới |
| **FR-AUTH-05** | C | Đơn đăng ký Owner phải gắn liền với việc chấp thuận điều khoản hệ thống trước khi duyệt |

### 9A.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của bản ghi điều khoản | Tự sinh bởi `uuid_generate_v4()` | — |
| `term_code` | `VARCHAR(50) NOT NULL` | Mã loại điều khoản (VD: `'OWNER_ONBOARDING'`, `'NEW_SITE_POLICY'`) | Do Quản trị viên/Hệ thống quy định | System Governance |
| `version` | `VARCHAR(20) NOT NULL` | Phiên bản điều khoản (VD: `'v1.0'`, `'v1.1'`) | Quản trị viên chỉ định khi ban hành văn bản mới | System Governance |
| `title` | `VARCHAR(255) NOT NULL` | Tiêu đề văn bản thỏa thuận / điều khoản | Quản trị viên nhập khi cấu hình | System Governance |
| `document_url` | `TEXT NOT NULL` | Đường dẫn URL tải file PDF/Doc trên Cloud Storage | Storage Service / CDN upload | System Governance |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` | Trạng thái hiệu lực hiện hành của điều khoản | Quản trị viên bật/tắt | System Governance |
| `effective_from` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm bắt đầu có hiệu lực pháp lý | Quản trị viên chọn hoặc mặc định thời điểm tạo | System Governance |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi | Tự sinh bởi database | — |

### 9A.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_system_terms_version` | `UNIQUE (term_code, version)` | Mỗi mã điều khoản chỉ có 1 bản ghi duy nhất cho mỗi phiên bản |

---

## 9B. Bảng `owner_applications` — Đơn đăng ký Chủ bãi xe

### 9B.1 Mục đích nghiệp vụ

Quản lý quy trình duyệt đơn đăng ký làm Owner. Theo §3.1.1, FR-AUTH-05 và quy trình nâng cấp Onboarding, Owner registration phải trải qua các bước: điền thông tin cơ bản, thiết lập cấu trúc bãi dự kiến (`draft_structure`), xác nhận đồng thuận với điều khoản (`terms_id`, `agreed_at`, `agreed_ip`), và phải có Admin approval trước khi tài khoản được kích hoạt cho các hoạt động Owner.

### 9B.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-05** | C | Admin duyệt Owner registration sau khi xác nhận đủ company name, email, phone; role changes remain audited |
| **§3.1.1** | — | Owner registration yêu cầu company name, email, phone; phải qua Admin approval trước khi active |
| **§2.3.1** | — | `ACCOUNT_ADMIN`: Admin approves Owner registration |
| **US-A01** | US | "Là Admin, tôi muốn phê duyệt hoặc từ chối đơn đăng ký chủ bãi xe" |

### 9B.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh đơn đăng ký | Tự sinh bởi `uuid_generate_v4()` | — |
| `user_id` | `UUID NOT NULL` | FK đến user nộp đơn | Gán bằng `users.id` của người nộp đơn | FR-AUTH-05 |
| `business_name` | `VARCHAR(255) NOT NULL` | Tên doanh nghiệp/công ty sở hữu bãi đỗ xe (§3.1.1 yêu cầu company name) | Do user nhập khi nộp đơn; phải trùng hoặc liên kết với `users.company_name` | FR-AUTH-05; §3.1.1 |
| `lot_type` | `VARCHAR(30) NOT NULL` | Loại bãi đỗ xe: `'outdoor'`, `'basement'`, `'multi-storey'` | Do Owner chọn trong form đăng ký | §3.2.1 |
| `terms_id` | `UUID NULL` | FK đến `system_terms_and_conditions.id` — phiên bản điều khoản mà Owner đã đọc và đồng thuận | Hệ thống gán ID điều khoản active tại thời điểm submit | System Governance |
| `agreed_at` | `TIMESTAMPTZ NULL` | Thời điểm Owner bấm đồng ý các điều khoản | Do Application gán `NOW()` khi user tick đồng ý | System Governance |
| `agreed_ip` | `VARCHAR(50) NULL` | Địa chỉ IP của Owner tại thời điểm xác nhận điều khoản (Audit Trail) | Client IP từ HTTP Request | §4.3 (Audit) |
| `tenant_id` | `UUID NULL` | FK liên kết đến `tenants.id` được tạo ra sau khi Admin duyệt đơn (`ON DELETE SET NULL`) | Gán bằng ID của tenant mới tạo khi duyệt đơn | FR-AUTH-05 |
| `site_id` | `UUID NULL` | Logical ID của bãi xe được khởi tạo ở trạng thái `PENDING_APPROVAL` bên Facility & Visualization Service | Application sinh trước hoặc lấy từ Facility Service | FR-LOT-01 |
| `draft_structure` | `JSONB NULL` | Snapshot cấu hình bãi dự kiến (Zone, Floor, Capacity) lúc đăng ký | Frontend gửi lên dưới dạng JSON | §3.2.3 |
| `status` | `VARCHAR(20) NOT NULL DEFAULT 'pending'` | Trạng thái xử lý đơn. `pending`: chờ Admin xem xét. `approved`: được chấp thuận → system kích hoạt role OWNER cho account. `rejected`: bị từ chối | `'pending'` khi tạo; Admin cập nhật thành `'approved'` hoặc `'rejected'` | FR-AUTH-05 |
| `submitted_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm nộp đơn chính thức | Do Application gán = `NOW()` khi user submit | FR-AUTH-05 |
| `reviewed_at` | `TIMESTAMPTZ NULL` | Thời điểm Admin xem xét và ra quyết định. `NULL` khi `status = 'pending'` | Do Application gán = `NOW()` khi Admin cập nhật `status` | FR-AUTH-05 |
| `reviewed_by` | `UUID NULL` | FK đến `users.id` của Admin đã xem xét đơn. `NULL` khi `status = 'pending'` | Gán bằng `users.id` của Admin đang thực hiện review | FR-AUTH-05; §2.3.1 |
| `review_note` | `VARCHAR(2000) NULL` | Ghi chú của Admin khi duyệt hoặc từ chối | Do Admin nhập khi review | FR-AUTH-05 |

### 9B.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_owner_applications_users` | `REFERENCES users(id)` | Khóa ngoại tới user nộp đơn |
| `uq_owner_applications_user` | `UNIQUE(user_id)` | Mỗi user chỉ có thể có một đơn đăng ký |
| `FK_owner_applications_reviewer` | `REFERENCES users(id)` | Khóa ngoại tới Admin duyệt đơn |
| `FK_owner_applications_terms` | `REFERENCES system_terms_and_conditions(id)` | Khóa ngoại tới điều khoản dịch vụ mà Owner đã đồng ý |
| `FK_owner_applications_tenants` | `REFERENCES tenants(id) ON DELETE SET NULL` | Khóa ngoại tới tenant tổ chức tạo sau khi duyệt |
| `idx_owner_applications_tenant_id` | `tenant_id` | Tra cứu đơn đăng ký theo tenant |
| `chk_owner_lot_type` | `CHECK (lot_type IN ('outdoor', 'basement', 'multi-storey'))` | Giới hạn loại bãi hợp lệ |
| `chk_owner_status` | `CHECK (status IN ('pending', 'approved', 'rejected'))` | Giới hạn trạng thái xử lý đơn |
| `chk_owner_review_consistency` | `CHECK ((status = 'pending' AND reviewed_at IS NULL AND reviewed_by IS NULL) OR (status <> 'pending' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))` | Bảo đảm tính nhất quán: đơn đang xử lý thì chưa có người duyệt, đơn đã xử lý thì phải có người duyệt |

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
| `account_id` | `UUID PK` | FK đến account Operator được cấp quyền. Dùng làm Primary Key vì mỗi account chỉ có một bộ `operator_grants` (1-1 relationship) | Gán bằng `accounts.id` của Operator account | FR-AUTH-04; §2.3.1 |
| `created_by` | `UUID NOT NULL` | FK đến `users.id` của Owner đã tạo grant | Gán bằng `users.id` của Owner đang thực hiện cấp quyền | FR-AUTH-04; §2.3.1 (audit) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cấp quyền; phục vụ audit trail | Tự sinh bởi database | §2.3.1; §4.3 |
| `permissions` | `TEXT[] NOT NULL` | Mảng các quyền vận hành được cấp cho Operator tại site | Owner chọn từ danh sách quyền hợp lệ khi tạo/cập nhật grant | FR-AUTH-04; FR-AUTH-06; §2.3.1 |

**Danh sách quyền và ý nghĩa:**

| Quyền | Ý nghĩa nghiệp vụ | Mapping SRS |
|---|---|---|
| `DEVICE_MANAGE` | Operator được phép quản lý thiết bị (camera, cổng, sensor) tại lot được phân công | §2.3.1 |
| `DEVICE_STATUS_VIEW` | Operator được phép xem trạng thái thiết bị tại lot được phân công | §2.3.1 |
| `CASH_COLLECT` | Operator được phép ghi nhận thu tiền mặt qua CASH_COLLECT interface | §2.3.1 |
| `APPEAL_REVIEW` | Operator được phép xem xét và phê duyệt khiếu nại của Driver | §2.3.1 |
| `SLOT_OVERRIDE` | Operator được phép can thiệp thủ công (override) trạng thái slot | §2.3.1 |
| `EMERGENCY_GATE_RELEASE` | Operator được phép mở cổng cưỡng bức trong tình huống khẩn cấp | §2.3.1 |
| `VIOLATION_REVIEW` | Operator được phép xác minh vi phạm đỗ xe tại bãi | §2.3.1 |

### 10.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_operator_grants_accounts` | `REFERENCES accounts(id) ON DELETE CASCADE` | Xóa account thì xóa grants |
| `FK_operator_grants_creator` | `REFERENCES users(id) ON DELETE RESTRICT` | Không cho xóa user nếu vẫn còn grant do user đó tạo |
| `chk_operator_permissions_count` | `CHECK (cardinality(permissions) BETWEEN 1 AND 7)` | Phải cấp ít nhất 1 quyền, tối đa 7 quyền (bằng tổng số quyền hợp lệ) |
| `chk_operator_permissions_no_null` | `CHECK (array_position(permissions, NULL) IS NULL)` | Không cho phép giá trị NULL trong mảng permissions |
| `chk_operator_permissions_valid` | `CHECK (permissions <@ ARRAY['DEVICE_MANAGE','DEVICE_STATUS_VIEW','CASH_COLLECT','APPEAL_REVIEW','SLOT_OVERRIDE','EMERGENCY_GATE_RELEASE','VIOLATION_REVIEW']::TEXT[])` | Chấp nhận 7 quyền hợp lệ theo ma trận phân quyền §2.3.1 |
| `idx_operator_grants_creator` | `created_by` | Tra cứu tất cả grants do một Owner cụ thể tạo ra |

---

## 11. Bảng `auth_challenges` — Lưu mã xác thực (OTP/Link)

### 11.1 Mục đích nghiệp vụ

Lưu trữ các mã xác thực (như OTP qua SMS/Email) được hệ thống gửi cho người dùng để xác nhận danh tính hoặc thiết lập MFA. Bảng này quản lý vòng đời của một mã OTP, bao gồm thời hạn, số lần thử sai để chống brute-force.

### 11.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-01** | C | Xác thực đa yếu tố và OTP |
| **§3.1.1** | — | OTP valid 5 phút, khóa sau 3 lần sai |

### 11.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK` | Định danh phiên xác thực | Tự sinh bởi Application | — |
| `user_id` | `UUID NOT NULL` | FK đến users(id) | Application truyền xuống | FR-AUTH-01 |
| `purpose` | `TEXT NOT NULL` | Mục đích OTP (ví dụ: login, verify_email) | Application truyền xuống | — |
| `channel` | `TEXT NOT NULL` | Kênh gửi (sms, email) | Do người dùng chọn hoặc system quyết định | — |
| `destination` | `TEXT NOT NULL` | Địa chỉ gửi (số điện thoại, email) | Lấy từ profile người dùng | — |
| `code_hash` | `TEXT NOT NULL` | Mã OTP đã băm | System sinh random, băm trước khi lưu | §4.3 |
| `expires_at` | `TIMESTAMPTZ NOT NULL` | Thời gian hết hạn | `NOW() + 5 phút` | §3.1.1 |
| `resend_at` | `TIMESTAMPTZ NOT NULL` | Thời gian cho phép gửi lại | `NOW() + 1 phút` | — |
| `attempts` | `INT NOT NULL DEFAULT 0` | Số lần nhập sai | Tăng 1 mỗi lần sai | §3.1.1 |
| `locked_until` | `TIMESTAMPTZ NULL` | Thời gian khóa tạm thời | Set khi attempts = 3 | §3.1.1 |
| `consumed_at` | `TIMESTAMPTZ NULL` | Thời gian nhập thành công | Set khi OTP khớp | — |

### 11.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_auth_challenges_users` | `REFERENCES users(id)` | Liên kết OTP với người dùng |
| `ix_auth_challenge_user` | `user_id, purpose` | Tìm phiên OTP của user nhanh chóng |

---

## 12. Bảng `workflow_deliveries` — Lịch sử gửi thông báo

### 12.1 Mục đích nghiệp vụ

Lưu vết (audit log) các tác vụ gửi tin nhắn, email chứa OTP. Đóng vai trò như một Outbox Pattern để theo dõi trạng thái gửi thành công/thất bại, hỗ trợ retry gửi lại khi có lỗi mạng từ Notification Provider.

### 12.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **§2.10.4** | — | Hỗ trợ Transactional Outbox pattern và Reliable messaging |

### 12.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK` | Định danh giao dịch gửi tin | Tự sinh | — |
| `user_id` | `UUID NOT NULL` | Người nhận tin | FK đến users(id) | — |
| `actor_id` | `UUID NOT NULL` | Người thực hiện hành động | FK đến users(id) | — |
| `kind` | `TEXT NOT NULL` | Loại thông báo (otp_email, otp_sms) | Application truyền xuống | — |
| `protected_payload` | `TEXT NOT NULL` | Nội dung gửi (đã mã hóa bảo mật) | Application sinh ra | — |
| `status` | `TEXT NOT NULL DEFAULT 'pending'` | Trạng thái | Workflow engine cập nhật | — |
| `attempts` | `INT NOT NULL DEFAULT 0` | Số lần thử gửi | Workflow engine cập nhật | — |
| `created_at` | `TIMESTAMPTZ NOT NULL` | Thời điểm tạo lệnh gửi | Tự sinh | — |
| `sent_at` | `TIMESTAMPTZ NULL` | Thời điểm gửi thành công | Cập nhật khi status = sent | — |
| `retry_at` | `TIMESTAMPTZ NULL` | Lịch hẹn gửi lại nếu failed | Tính toán bởi backoff logic | — |
| `expires_at` | `TIMESTAMPTZ NULL` | Thời điểm lệnh gửi bị hủy | — | — |
| `challenge_id` | `UUID NULL` | FK đến auth_challenges(id) | Liên kết lệnh gửi này phục vụ phiên OTP nào | — |

### 12.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_workflow_deliveries_users` | `REFERENCES users(id)` | Liên kết lịch sử với người dùng |
| `FK_workflow_deliveries_actor` | `REFERENCES users(id)` | Liên kết lịch sử với người thực hiện |
| `FK_workflow_deliveries_challenge` | `REFERENCES auth_challenges(id)` | Khóa ngoại tới auth_challenges |
| `chk_status_valid` | `CHECK(status IN ('pending','failed','sent','cancelled'))` | Giới hạn trạng thái hợp lệ |
| `ix_workflow_delivery_due` | `status, retry_at` | Truy vấn nhanh các lệnh cần gửi lại |

---

## 14. Bảng `mfa_credentials` — Cấu hình bảo mật 2 lớp

### 13.1 Mục đích nghiệp vụ

Lưu trữ cấu hình MFA (Multi-Factor Authentication) của người dùng, đặc biệt là mã khóa bí mật TOTP (Time-based One-Time Password) để sinh mã qua ứng dụng Authenticator.

### 13.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-AUTH-01** | C | Xác thực đa yếu tố |
| **§3.1.4** | — | Multi-factor authentication (MFA) dùng TOTP |

### 13.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `user_id` | `UUID PK` | Người dùng sở hữu cấu hình MFA | Khóa chính đồng thời là FK tới users | FR-AUTH-01 |
| `protected_secret` | `TEXT NOT NULL` | Khóa bí mật TOTP, đã được mã hóa | Sinh random tại Application và mã hóa | §3.1.4 |
| `enabled` | `BOOL NOT NULL DEFAULT FALSE` | MFA có đang bật không | Cập nhật khi user verify thành công code đầu tiên | — |
| `last_step` | `BIGINT NOT NULL DEFAULT -1` | Timestamp của mã TOTP cuối cùng đã dùng (ngừa replay) | Application cập nhật | — |
| `attempts` | `INT NOT NULL DEFAULT 0` | Số lần nhập sai mã TOTP | Tăng 1 khi sai | — |
| `locked_until` | `TIMESTAMPTZ NULL` | Khóa tạm thời nếu nhập sai nhiều lần | Tính toán bởi Application | — |

### 13.4 Constraints & Indexes

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `FK_mfa_credentials_users` | `REFERENCES users(id)` | Ràng buộc 1-1 với user |

---

## 15. Tóm tắt quan hệ giữa các bảng

```text
users (1) ──── (N) accounts
users (1) ──── (0..1) driver_registrations
users (1) ──── (0..1) owner_applications
users (1) ──── (N) user_refresh_tokens
users (1) ──── (N) auth_challenges
users (1) ──── (0..1) mfa_credentials

tenants (1) ──── (0..N) accounts  [tenant_id FK nội bộ]
tenants (1) ──── (0..N) owner_applications  [tenant_id FK nội bộ]

system_terms_and_conditions (1) ──── (0..N) owner_applications  [terms_id]

accounts (1) ──── (N) account_roles
accounts (N) ──── (N) roles  [qua account_roles]
accounts (1) ──── (N) vehicles  [account_id trong vehicles]
accounts (1) ──── (0..1) operator_grants

operator_grants.created_by ──── users.id  [audit FK]
owner_applications.reviewed_by ──── users.id [audit FK]
```

---

## 16. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `site_id` | `accounts` | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID phạm vi gán Operator; đồng bộ trạng thái qua RabbitMQ |
| `site_id` | `owner_applications` | Facility & Visualization (`FacilityDb.parking_sites.id`) | Logical ID bãi xe khởi tạo ở trạng thái `PENDING_APPROVAL`, active sau khi duyệt |
| `normalized_plate` | `vehicles` | Operations, IoT & Recognition | RabbitMQ event `VehicleRegistered` / `VehiclePlateUpdated`; consumer cache plate |
| `account_id` | `vehicles` | Operations, Payment & Pricing | Logical ID; consumer services gọi User & Access API để resolve thông tin đầy đủ khi cần |

---

## 17. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key |
