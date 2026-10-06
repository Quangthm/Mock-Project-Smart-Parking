# Chạy và kiểm tra login/logout theo API design

Cập nhật 06/10/2026. Bảng đối chiếu và quyết định: [auth design alignment](../02-architecture/api/auth-design-alignment-2026-10-06.md).

## Chuẩn bị PostgreSQL

Dự án dùng PostgreSQL tại `localhost:5433` theo `ConnectionStrings:DefaultConnection`. Với database mới:

```powershell
docker compose up -d postgres-db
```

Với volume/database đã tồn tại, chạy `scripts/database/05.2-Auth-Design-Alignment.sql` trước khi chạy API. Script initialization không tự chạy lại khi volume đã có dữ liệu. Không xóa volume để migration.

Ví dụ cho container của compose hiện tại:

```powershell
Get-Content -Raw scripts/database/05.2-Auth-Design-Alignment.sql | docker exec -i smartparking_db psql -U smartpark_user -d smartpark_db -v ON_ERROR_STOP=1
```

Seed mới dùng bcrypt cost 12: `driver@gmail.com / Password@123`. Nếu seed cũ vẫn plaintext, có script riêng `05.3-Demo-Password-Hash.sql` chỉ đổi đúng fixture có ID/email/password demo đã biết. Những user plaintext khác cần quy trình reset/chuyển đổi có kiểm soát. API không chấp nhận plaintext fallback.

## Khóa ký RS256

Production cần `Jwt:PrivateKeyPem` từ cấu hình ngoài repository (biến môi trường `Jwt__PrivateKeyPem` hoặc secret store). Issuer/audience phải thống nhất giữa các instance. Không commit private key.

```powershell
$env:Jwt__PrivateKeyPem = Get-Content -Raw -LiteralPath 'D:\Secrets\smartpark-jwt-private.pem'
```

Development có thể chạy không cấu hình key: API tự tạo RSA 2048-bit cho tiến trình đó. Khi dùng key tạm, restart sẽ làm token cũ không xác thực được. Muốn kiểm tra phiên qua restart/nhiều instance, cấu hình cùng RSA private key. Phiên/refresh token vẫn được lưu PostgreSQL.

## Chạy API và frontend

```powershell
dotnet restore SmartParking.slnx
dotnet run --project src/Services/UserService/SmartParking.UserService.API --launch-profile http
```

Chạy frontend bằng workflow Vite hiện tại. API mặc định `http://localhost:5035`; frontend hỗ trợ `VITE_API_BASE_URL`. CORS cho localhost/127.0.0.1 cổng 5173.

## Contract

| Endpoint | Request | Thành công | Lỗi |
| --- | --- | --- | --- |
| POST /api/auth/login | JSON email/password | 200; accessToken, refreshToken, expiresIn=3600, tokenType=Bearer, user | 400 validation; 401 AUTH_FAILED; 403 ACCOUNT_LOCKED |
| GET /api/auth/me | Bearer access token | 200; dữ liệu user hiện tại | 401 INVALID_TOKEN |
| POST /api/auth/logout | Bearer access token + JSON refreshToken | 200; success=true, message | 400 MISSING_REFRESH_TOKEN; 401 INVALID_TOKEN; 500 REVOKE_SESSION_FAILED |
| POST /api/auth/refresh | JSON refreshToken | 200; cặp token mới và user | 400 MISSING_REFRESH_TOKEN; 401 INVALID_TOKEN |

Logout mẫu:

```json
{ "refreshToken": "<refreshToken từ login>" }
```

Lỗi có dạng `{ "success": false, "code": "AUTH_FAILED", "message": "Invalid email or password." }`. Refresh endpoint/envelope là lựa chọn triển khai cần so lại khi có API design gốc.

Refresh có hạn tuyệt đối 7 ngày. Rotation đổi cả refresh hash và access jti; cặp cũ không dùng lại được. Logout chỉ thu hồi đúng phiên có cặp token tương ứng, không thu hồi phiên thiết bị khác. Token của phiên khác không được dùng để logout. Database lưu hash, không lưu refresh token gốc.

Frontend giữ access/refresh trong sessionStorage và gửi refreshToken khi Sign Out. Khi hết access token, frontend hiện yêu cầu đăng nhập lại; chưa tự gọi refresh. F5 xác minh user bằng `/me`. Sign Up và dữ liệu nghiệp vụ frontend vẫn là mock.

Nếu logout trả 401, frontend kiểm tra lại `/me`: bearer còn hợp lệ thì giữ token và báo lỗi logout; `/me` trả 401 thì xóa phiên cục bộ và chuyển sang đăng nhập. Lỗi logout không được coi là thu hồi phiên thành công. Nếu kiểm tra `/me` lỗi mạng/server, token được giữ để thử lại.

## Kịch bản kiểm tra

1. Sai password trả 401/AUTH_FAILED; lần sai thứ ba trả 403/ACCOUNT_LOCKED. Password đúng vẫn bị chặn trong thời gian khóa.
2. Sau khóa 15 phút, login đúng reset counter; khóa admin không có locked_until không tự mở.
3. Login trả access 1 giờ, refresh thực, role từ backend và không trả password hash.
4. Bearer đúng nhưng logout thiếu body/refresh trả 400/MISSING_REFRESH_TOKEN và phiên vẫn dùng được.
5. Logout đúng trả 200; `/me` và refresh với token cũ bị 401/INVALID_TOKEN.
6. Refresh tạo cặp mới; access cũ và refresh replay đều bị 401. Hai request refresh cùng snapshot chỉ một request thành công.
7. Tài khoản bị khóa/xóa/đổi role không tiếp tục truy cập qua JWT cũ.
8. Khi database lỗi lúc thu hồi, API trả REVOKE_SESSION_FAILED và frontend giữ trạng thái để thử lại.
9. User cũ đã soft delete không cản user mới đăng nhập bằng cùng email; chỉ user chưa xóa được chọn.
10. Logout sai cặp refresh trả 401 nhưng bearer vẫn dùng được; frontend báo lỗi và giữ phiên, không báo logout thành công.

Import `docs/04-testing/postman/UserService.postman_collection.json`. Collection kiểm tra contract mới và giữ token trong biến runtime. Mỗi lần chạy collection có một login sai trên fixture; login đúng lần sau reset counter. Không chạy lặp request sai tới ngưỡng rồi kỳ vọng login thành công ngay.

## Kiểm chứng

```powershell
dotnet test SmartParking.slnx -c Release
node --test tests/frontend/auth-api.test.cjs
Set-Location FE
npm.cmd run build
```

Review sau sửa ngày 06/10/2026: **54/54 test backend đạt, 0 skip** (51 ca auth/unit/HTTP, 1 scaffold có sẵn, 2 test PostgreSQL thật); **9/9 test hồi quy frontend đạt**, frontend build đạt. Test HTTP dùng TestHost và adapter lưu phiên riêng; hai test PostgreSQL dùng Docker PostgreSQL 16 để kiểm tra migration/concurrency/rotation/revocation qua DbContext mới và login khi email được dùng lại sau soft delete. Mỗi test tự tạo/xóa database riêng, tách khỏi dữ liệu dự án. Các test frontend dùng Node có sẵn và TypeScript trong `FE/node_modules`, cần cài dependency frontend trước khi chạy.

Chạy Release giúp tránh ghi đè DLL Debug đang được Visual Studio giữ. Để chạy đủ 54 test, đặt `SMARTPARK_AUTH_TEST_CONNECTION` theo hướng dẫn bên dưới; không có biến này thì hai test PostgreSQL sẽ skip. Microsoft.OpenApi đã dùng bản vá 2.7.5; endpoint `/openapi/v1.json` được kiểm tra trả 200. Luồng HTTP thật đạt login 200, `/me` 200, logout sai refresh 401 vẫn giữ phiên, logout đúng 200 và token đã thu hồi 401. Cảnh báo bundle frontend lớn vẫn còn.

Còn ngoài phạm vi: OTP, MFA, provisioning/registration backend, phân quyền nghiệp vụ toàn hệ thống, tự động refresh frontend. SRS còn ghi session 24 giờ; tác vụ này ưu tiên access 3600 giây trong báo cáo/API design.

## Test PostgreSQL riêng

Docker không bắt buộc: có thể dùng PostgreSQL cài trực tiếp hoặc server test khác. Trong lần kiểm tra 06/10/2026 đã dùng container Docker riêng. Khởi tạo PostgreSQL test ở cổng 65433 (không dùng volume dự án):

```powershell
docker run --detach --rm --name smartpark-auth-test --publish 127.0.0.1:65433:5432 --env POSTGRES_USER=auth_test --env POSTGRES_PASSWORD=auth_test --env POSTGRES_DB=postgres postgres:16-alpine
docker exec smartpark-auth-test pg_isready -U auth_test -d postgres
```

Khi `pg_isready` báo accepting connections, đặt connection string tới server cho phép tạo database tạm:

```powershell
$env:SMARTPARK_AUTH_TEST_CONNECTION = 'Host=127.0.0.1;Port=65433;Database=postgres;Username=auth_test;Password=auth_test'
dotnet test SmartParking.slnx --filter FullyQualifiedName~PostgresAuthTests
```

Mỗi test tự tạo database `smartpark_auth_test_<random>` và chỉ xóa database đó sau khi chạy. Bộ test kiểm tra migration chạy hai lần, chuyển seed plaintext đã biết, ba request sai đồng thời, rotation đồng thời, revoke, đọc phiên từ DbContext mới và email được dùng lại sau soft delete. Khi không đặt biến môi trường, các test này được đánh dấu skip có lý do, không tính là pass.

Sau khi chạy, dọn container test:

```powershell
docker stop smartpark-auth-test
Remove-Item Env:SMARTPARK_AUTH_TEST_CONNECTION -ErrorAction SilentlyContinue
```

`--rm` dọn container/volume tạm khi stop. Không chạy các lệnh dọn này với container `smartparking_db` hoặc volume dự án.
