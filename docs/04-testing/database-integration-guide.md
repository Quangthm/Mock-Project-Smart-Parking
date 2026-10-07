# Code develop sử dụng database của arch/database-schema

`develop` giữ code API/FE, service adapters, tests và scripts vận hành.
`arch/database-schema` giữ SQL baseline của thành viên, ba overlay tích hợp,
compose database và tài liệu database. Bản database tương thích ban đầu là
commit `6797923`; sáu baseline SQL gốc vẫn giữ nguyên từ `69d4da2`.

Code kết nối các PostgreSQL instances đã dựng qua ConnectionStrings. Nhánh Git
là nơi quản lý định nghĩa schema; không phải địa chỉ kết nối runtime. Không
merge cả code develop vào nhánh database và không sao chép SQL canonical vào
develop cho bản tích hợp này.

## Dựng hai phần cùng nhau

Trong terminal tại checkout code `develop`, tạo checkout database riêng (nếu
đã có checkout thì dùng đường dẫn đó, không tạo lại):

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

Compose khởi tạo DB mới bằng baseline + overlay đúng service. Không apply các
SQL monolith `05.*` trên develop hoặc seed Sprint 2 cũ vào topology này. Nếu
đã có dữ liệu/volumes thì không chạy lại baseline CREATE TABLE; cần migration
riêng. Hướng dẫn chi tiết overlay, dữ liệu và SRS nằm trong
`docs/02-architecture/database/schema-integration.md` ở checkout database.

Mở ba terminal tại checkout code `develop`, đặt các biến cấu hình trên trong
mỗi terminal và chạy mỗi service riêng:

```powershell
./scripts/run-schema-service.ps1 -Service Reservation
./scripts/run-schema-service.ps1 -Service Parking
./scripts/run-schema-service.ps1 -Service User
```

| Service trên develop | API port | PostgreSQL từ nhánh database | Connection string |
|---|---|---|---|
| User | 5035 | 5441 / smartpark_user | ConnectionStrings:User |
| Parking | 5045 | 5442 / smartpark_parking | ConnectionStrings:Parking |
| Reservation safety adapter | 5055 | 5443 / smartpark_reservation | ConnectionStrings:Reservation |

SMARTPARK_SCHEMA_ROOT dùng cho test fixtures và recovery tìm checkout schema.
API không đọc Git/SQL branch lúc xử lý request và không tự tạo schema thay cho
thành viên database. Các service không truy cập DB của service khác.

FE dùng VITE_API_BASE_URL và VITE_PARKING_API_BASE_URL trong
`FE/.env.schema.example`; giữ server FE đã chạy, hoặc dùng `npm run dev` khi
chưa có server. Local CORS hiện cho phép port 5173.

Admin Development `admin@smartpark.local` dùng SMARTPARK_ADMIN_PASSWORD (8–15
ký tự, hoa/thường, số, ký tự đặc biệt). Bootstrap chỉ tạo mới, không thay password
hay nâng quyền user thường. Tạo Owner → Admin duyệt → tạo site/unit/slot → tạo
Operator bằng API/UI thật. OTP đăng ký Development log code; production cần
delivery và RSA signing key cấu hình riêng.

## Kiểm tra

Đặt SMARTPARK_SCHEMA_ROOT tới checkout database trước khi chạy DB tests. Các
tests đọc SQL trực tiếp từ đó, chạy ba API với ba PostgreSQL databases riêng và
không dùng EF EnsureCreated để tự sinh schema. SMARTPARK_AUTH_TEST_CONNECTION
phải trỏ PostgreSQL test có quyền tạo/xóa các database test UUID riêng.

```powershell
$env:SMARTPARK_SCHEMA_ROOT = $schemaRoot
$env:SMARTPARK_AUTH_TEST_CONNECTION = 'Host=127.0.0.1;Port=65433;Database=postgres;Username=auth_test;Password=auth_test'
dotnet test SmartParking.slnx --no-restore
# terminal FE
npm run build
```

Recovery script nằm trên develop. Khi có durable hold do commit/HTTP timeout,
dừng tất cả Parking writers, giữ Reservation/API DB chạy, đặt SchemaRoot rồi
inspect trước:

```powershell
./scripts/recover-structure-hold.ps1 -SchemaRoot $schemaRoot -SiteId '<uuid>' -ParkingStopped
# Chỉ thêm -Apply sau khi đối chiếu authoritative snapshot khi Parking vẫn dừng.
```

Không tự xóa/expire hold hay release outcome=null sau ambiguous commit. Công cụ
từ chối khi Parking DB còn connection khác và publish đầy đủ trạng thái,
tombstones, capacity trước khi release.

## Push hai phần

Mỗi nhánh có một commit riêng cho phần mình sở hữu. Push database trước, rồi code:

```powershell
git push origin arch/database-schema
git push origin develop
```

Hai lệnh dùng branch refs nên không cần đổi checkout để push. Không dùng force
push. Nếu remote đã thay đổi, cập nhật và tích hợp thay đổi của nhóm trước khi
push. Sau khi nhận cả hai nhánh, dựng/applied schema trước khi khởi động code.

## Phạm vi kiểm chứng và giới hạn

Bản tích hợp trước khi tách đã pass 74/74 backend tests, FE build, smoke qua ba
cổng API và recovery inspect/apply. Sau khi tách, ngày 2026-10-07 kiểm tra lại:

- 74/74 backend tests pass, 0 skip, code develop đọc SQL trực tiếp từ checkout
  arch/database-schema riêng bằng SMARTPARK_SCHEMA_ROOT.
- FE build pass; compose ở checkout database init/healthcheck ba DB pass.
- Recovery script develop đọc compose từ checkout database qua env và tham số,
  inspect/apply pass; capacity và quota cập nhật về số slot thật rồi release hold.
- Agent review xác nhận hai nhánh chỉ nhận phần thay đổi riêng, không mất code
  đã review, không đổi sáu baseline SQL và không có finding P1/P2 mới do tách.

FE browser QA chưa thực hiện; build còn cảnh báo bundle lớn. Các API/DB và
checkout phục vụ kiểm tra được dọn sau khi hoàn tất.

User/Owner/Admin/Operator và parking core được nối database trong phạm vi các
task đã làm. OTP login/TOTP MFA, reallocation/refund đầy đủ theo FR-LOT-04 và
một số editor FE vẫn là phần triển khai tiếp; password login và các kiểm tra
bảo thủ không có nghĩa là toàn bộ SRS đã hoàn thành.
