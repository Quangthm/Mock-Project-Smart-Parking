# Tích hợp các task hiện có với database của nhóm

Baseline: sáu SQL trong `scripts/database/microservices/` giữ nguyên nội dung trên `arch/database-schema` tại `69d4da2` (schema commit `d0bbca6`). Code ứng dụng và các overlay tích hợp dùng baseline này, đối chiếu nghiệp vụ với `docs/01-requirements/srs/smartpark-srs-v0.9.md`, solution architecture và database architecture trong cùng thư mục.

## Hai nhánh phối hợp

`develop` giữ code ứng dụng, adapters HTTP, FE, tests và scripts chạy/khôi phục. `arch/database-schema` giữ schema chính, overlay SQL, compose database và tài liệu database. Không merge code ứng dụng vào nhánh database để triển khai bản tích hợp này. Mỗi phần có commit và push riêng; thay đổi schema phải được áp dụng trước khi chạy code cần schema đó.

Dùng `compose.schema-integration.yml` trong checkout `arch/database-schema` để dựng các database riêng. API trên `develop` kết nối PostgreSQL qua ConnectionStrings, không kết nối trực tiếp một nhánh Git. Biến `SMARTPARK_SCHEMA_ROOT` chỉ giúp tests và công cụ recovery tìm đúng checkout chứa SQL/compose. `scripts/database/docker-compose-smartpark.yml` là sandbox PostgreSQL cũ, không tự dựng topology ba database của bản tích hợp.

## Phạm vi và ownership

| Phần đã nối | Database/API chịu trách nhiệm | Hành vi |
|---|---|---|
| Login/logout, Driver đăng ký OTP | User | Users, accounts, roles, sessions, OTP và trạng thái hiện tại; access token 24 giờ, refresh 7 ngày |
| Owner đăng ký và Admin duyệt | User → Parking API | Pending approval không được login; approve tạo tenant với ID cố định bằng application ID, rồi kích hoạt Owner account; retry không tạo tenant trùng |
| Admin quản lý users | User | Tìm kiếm, khóa/mở khóa; session đã revoke không hồi sinh khi mở khóa |
| Owner quản lý bãi/cấu trúc | Parking → User API + Reservation API | Xác thực Owner hiện tại, tenant và resource scope; CRUD layout, enum và trạng thái theo schema |
| Owner tạo Operator | User → Parking API | Mỗi site có account SITE_OPERATOR và grant riêng; chỉ site active thuộc tenant được cấp; permission check đọc grant hiện tại |
| Operator đăng nhập | User | `/api/operators/me` trả từng site và permissions còn hiệu lực; FE hiển thị assignment thật |

Production service không tham chiếu persistence/domain của service khác, không join DB khác. Các project references xuyên service trong integration tests chỉ phục vụ chạy các host thật cùng test process.

Không áp dụng SQL monolith `05.*` hoặc seed cũ lên các DB này. Seed trong nhánh database có password placeholder, thiếu role mapping và capacity insert thiếu vehicle type, nên không dùng làm nguồn kiểm chứng login. Admin phát triển được tạo bằng BCrypt qua opt-in configuration, rồi đăng ký/duyệt Owner và tạo site qua API thật.

## Schema bổ sung

Chạy baseline một lần trên DB mới, rồi overlay đúng service:

- User: `integration/01-user-service-alignment.sql` thêm `PENDING_VERIFICATION`, `REJECTED` cho users và bảo đảm role dictionary. Accounts dùng status có sẵn; rejection account là INACTIVE. Account context được xác định bằng account_roles, không có account_type/membership_tier/loyalty_points.
- Parking: `integration/02-parking-service-alignment.sql` thêm tọa độ layout JSON, bảo vệ slot đã xóa và uniqueness code theo site. `physical_state` và `reservation_state` tách biệt; occupied/unknown/reserved/protected/backup không được xóa hay chuyển tùy ý.
- Reservation: `integration/03-reservation-structure-safety.sql` tạo durable fence và projection trạng thái site/resource đã xóa. Trigger chỉ tác động DB Reservation. Booking/allocation/session/pool writers phải đi qua fence này; không disable trigger trong các service tương lai.

Overlay chạy lặp lại được. Baseline SQL của thành viên nhóm là CREATE TABLE, không phải migration cho database đã có dữ liệu. Không chạy baseline lên volume/DB cũ và không dùng EF EnsureCreated để thay thế baseline. Nếu đã có dữ liệu production, cần migration/backfill riêng sau khi kiểm kê dữ liệu; hướng dẫn dưới đây tạo môi trường phát triển độc lập.

## Chạy local trên Windows PowerShell

Yêu cầu Docker Desktop đang chạy, .NET 10 SDK và dependency FE đã cài. Compose này có project/volumes/ports riêng, không sửa container `smartparking_db` cũ.

Giữ hai checkout riêng để không phải đổi branch khi đang chạy. Trong terminal tại checkout code `develop`, tạo worktree database và đặt cấu hình local (không commit giá trị thật):

```powershell
$codeRoot = (Get-Location).Path
$schemaRoot = Join-Path (Split-Path $codeRoot -Parent) 'SmartParking-Database'
git worktree add $schemaRoot arch/database-schema
$env:SMARTPARK_SCHEMA_ROOT = $schemaRoot
$env:SMARTPARK_DEV_PASSWORD = 'local-db-password'
$env:SMARTPARK_SERVICE_KEY = 'local-internal-service-key-at-least-32-characters'
$env:SMARTPARK_ADMIN_PASSWORD = 'Password@123'
docker compose -f "$schemaRoot/compose.schema-integration.yml" up -d --wait
dotnet restore SmartParking.slnx
```

Mở ba terminal tại checkout code `develop`, đặt các biến trên (SMARTPARK_SCHEMA_ROOT là đường dẫn tuyệt đối tới checkout database) trong mỗi terminal, chạy mỗi service riêng. Scripts dưới đây nằm trên `develop`:

```powershell
./scripts/run-schema-service.ps1 -Service Reservation
./scripts/run-schema-service.ps1 -Service Parking
./scripts/run-schema-service.ps1 -Service User
```

Ports: User 5035 → DB 5441; Parking 5045 → DB 5442; Reservation 5055 → DB 5443. `ConnectionStrings:User/Parking/Reservation` chỉ cấp cho host tương ứng. User vẫn đọc `DefaultConnection` để tương thích cấu hình cũ, nhưng phải trỏ vào User DB riêng. Services:Key giống nhau ở ba host; internal endpoints không nhận bearer người dùng để thay thế service credential.

Nếu FE đã có server đang chạy thì dùng server đó. Nếu chưa có, terminal FE dùng `npm run dev`. `FE/.env.schema.example` cho biết hai API URL; sao chép thành `.env.local` khi cần, không ghi đè cấu hình local hiện có. Nếu dùng port khác 5173 cần bổ sung CORS origin trong host tương ứng.

Admin local: `admin@smartpark.local`, password lấy từ SMARTPARK_ADMIN_PASSWORD. Bootstrap chỉ hoạt động trong Development; không thay password của Admin đã tồn tại và không nâng quyền user thường trùng email. Driver đăng ký bằng OTP: Development bật log code; không dùng chế độ này ngoài local. RSA Development tự sinh key khi restart nên session cũ phải đăng nhập lại. Production cần Jwt:PrivateKeyPem và cấu hình OTP delivery thật.

## Luồng xác nhận

1. Đăng ký Owner, xác nhận chưa thể login; Admin duyệt đơn.
2. Owner login, tạo bãi, floor/zone/block, slot CAR/MOTORCYCLE/OVERSIZED, sửa profile và refresh để kiểm tra persisted state.
3. Tạo Operator với site và permissions; đăng nhập tài khoản mới, kiểm tra assignment theo từng site. Site/tenant khác bị từ chối.
4. Khóa user từ Admin; token đã cấp bị từ chối ở User và Parking. Mở khóa cần login lại.
5. Booking/session/allocation live, physical OCCUPIED/UNKNOWN hoặc reservation_state ngăn mutation ảnh hưởng quyền đã nhận. Không tự đẩy xe ra khỏi bãi.

Owner FE hiện nối list/create/update profile, activate/deactivate, create/remove units/slots và create/list Operators. Rename/capacity/move/configure slot/access paths có API backend nhưng chưa có editor FE hoàn chỉnh. Legacy dashboard Driver và các nghiệp vụ gate/payment/device chưa nằm trong tích hợp này.

Public Parking API: GET/POST `/api/parking-lots`; GET `/{id}/structure`; POST `/{id}/units`, `/{id}/slots`, `/{id}/paths`; PATCH `/{id}/structure`. PATCH action: profile, active, renameUnit, capacity, removeUnit, removeSlot, moveSlot, configureSlot, removePath. DTO và validation là nguồn chi tiết tại Parking API Program và ParkingStructure domain. Scope Owner lấy từ bearer hiện tại qua User, không nhận ownerId tùy ý từ FE.

## Fence và phục hồi lỗi

Parking mutation lấy hold tại Reservation, commit transaction Parking, rồi publish site state, tombstones và capacity snapshot trước khi release. Capacity pools có counter bằng 0 không được coi là accepted commitment chỉ vì total_capacity dương. Các pool đã tồn tại được cập nhật theo số slot vật lý CAR/MOTORCYCLE của site/unit subtree; giảm quota bằng min(quota cũ, capacity mới), không tự tăng quota. Site inactive bị chặn nhận commitment mới bằng projection active=false; configured capacity/quota được giữ để mở lại không mất quota. Không tự tạo pool/quota thương mại mới. Reservation baseline chưa hỗ trợ OVERSIZED bookings; Parking vẫn hỗ trợ layout OVERSIZED đúng baseline.

Nếu timeout/restart xảy ra sau acquire hoặc trong commit/release, hold được giữ nguyên. Các request mới có thể nhận 409/503 và Reservation write nhận 55P03. Không tự hết hạn hoặc xóa hold vì transaction Parking có thể đã commit. Phục hồi local bằng script có đối chiếu authoritative Parking:

1. Dừng **tất cả** Parking API instances; giữ Reservation API và các DB chạy. Không cho writer khác sửa Parking DB trong suốt recovery.
2. Inspect: `./scripts/recover-structure-hold.ps1 -SiteId '<uuid>' -ParkingStopped`. Script từ chối nếu DB Parking còn connection khác; đọc token của hold, tenant và trạng thái/soft-deleted resources/capacity trực tiếp từ Parking.
3. Đối chiếu snapshot được in ra. Nếu tenant đúng và state phản ánh kết quả đã commit, chạy lại thêm `-Apply`. Script publish outcome đầy đủ qua Reservation API cùng token; capacity/projections/release commit atomically trong Reservation DB.
4. Chỉ restart Parking sau khi script báo đã release. Nếu SQL/projection lỗi hoặc tenant sai, giữ hold và điều tra; không gọi release outcome=null hay DELETE thủ công. Release không outcome chỉ dành cho request chưa bắt đầu commit.

Script recovery nằm trên `develop`, mặc định inspect-only và tìm compose qua SMARTPARK_SCHEMA_ROOT hoặc tham số -SchemaRoot. Deployment khác cần công cụ vận hành tương đương: quiesce mọi Parking writer, kiểm tra hết transaction, snapshot authoritative rồi publish cùng hold token. Đây chưa phải distributed transaction/outbox tự phục hồi. Reservation API mới chỉ là adapter đảm bảo an toàn cấu trúc, chưa triển khai toàn bộ booking/check-in/payment.

## Giới hạn so với toàn bộ SRS

- Password login hiện hữu chưa có OTP login và TOTP MFA của §3.1.4. OTP đăng ký Driver không thay thế OTP authentication.
- FR-LOT-04 hiện ngăn mutation khi có cam kết đang hoạt động. Reallocation theo priority, reason/audit nghiệp vụ, unfulfillable/refund workflow vẫn cần Reservation/Payment tasks tương ứng; không mô tả chúng là đã hoàn thành.
- Cấu hình giờ mở/đóng, pricing, bản đồ frontend và các nghiệp vụ vận hành khác cần contract/API riêng. Imported database docs là baseline thiết kế; RLS không được bật tự động chỉ vì tài liệu mô tả nó.

## Kiểm tra đã thực hiện

Integration tests dùng baseline SQL thật và overlay, không tự sinh schema: User/Owner/Operator/Admin regression, ba API + ba DB riêng, current-state authorization/revocation và Parking + Reservation concurrency/rollback/tombstone/capacity. Chạy DB tests với SMARTPARK_AUTH_TEST_CONNECTION trỏ server PostgreSQL test có quyền tạo/xóa **database test UUID riêng**. Không dùng production credential.

```powershell
$env:SMARTPARK_SCHEMA_ROOT = $schemaRoot
$env:SMARTPARK_AUTH_TEST_CONNECTION = 'Host=127.0.0.1;Port=65433;Database=postgres;Username=auth_test;Password=auth_test'
dotnet test SmartParking.slnx --no-restore
# terminal FE
npm run build
```

Kết quả ngày 2026-10-07:

- Backend: 74/74 pass (63 User, 11 Parking), 0 skip; có PostgreSQL thật trong lần chạy cuối.
- FE: `npm run build` pass; Vite còn cảnh báo bundle lớn. Chưa thực hiện QA giao diện trong browser.
- Compose config + ba DB init/healthcheck pass; cả ba script service khởi động được.
- Smoke qua HTTP localhost: Admin bootstrap/login, Owner register/approve/login, create site/unit/slot persisted.
- Recovery từ chối khi Parking còn connection; inspect và apply thành công sau khi dừng Parking.
- `git diff --check` pass. Môi trường DB/API test do tác vụ dựng đã dọn, database cũ không bị migration.

Agent review độc lập đã đối chiếu SRS, kiến trúc và baseline SQL. Mapping Account, FE edit profile/stale parent, capacity pools, quota giữ qua đóng/mở, bootstrap validation và recovery runbook/tool được chỉnh theo review. Kết luận review cuối: không còn finding P1/P2 chưa xử lý trong phạm vi tích hợp; các giới hạn SRS phía trên vẫn còn.

SQL baseline và các tài liệu database chính của thành viên giữ nguyên. Các kết quả kiểm tra trên áp dụng cho code tích hợp và schema phối hợp; sau khi tách hai nhánh, kiểm tra lại code trên develop đọc SQL từ checkout arch/database-schema bằng SMARTPARK_SCHEMA_ROOT. Không tạo lịch sử merge code vào nhánh database trong cặp commit bàn giao.
