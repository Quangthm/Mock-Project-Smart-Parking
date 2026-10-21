# Chạy test PostgreSQL và RabbitMQ trên máy local

Các test tích hợp cần PostgreSQL thật để kiểm tra khóa ngoại, transaction và rollback.
`SMARTPARK_AUTH_TEST_CONNECTION` là chuỗi kết nối cho test UserService;
`SMARTPARK_STRUCTURE_TEST_CONNECTION` dành cho test ParkingService.
Đây là cấu hình của tiến trình test, không phải API key. Thiếu biến thì test tự **Skipped** (biểu tượng vàng).

## Chuẩn bị một lần trên mỗi máy

1. Cài .NET 10 SDK; chuẩn bị `.env` và PostgreSQL theo setup của project.
2. Khởi động `user-db` và `parking-db` trong `compose.schema-integration.yml`.
3. Tại thư mục gốc repo, chạy:

   ```powershell
   docker compose -f compose.tests.yml up -d --wait
   .\scripts\configure-test-settings.ps1
   ```

Script đọc `.env` của máy hiện tại, lấy `SMARTPARK_DEV_PASSWORD` và các port
`SMARTPARK_DB_USER_PORT` / `SMARTPARK_DB_PARKING_PORT` (mặc định 5441 / 5442),
rồi tạo `smartpark.local.runsettings`. Không cần thêm biến hệ thống hoặc tự nhập lại mật khẩu.
File sinh ra chứa thông tin kết nối: được Git ignore, mỗi thành viên tự tạo trên máy mình.
Chạy lại script nếu đổi mật khẩu hoặc port.

`compose.tests.yml` khởi động RabbitMQ riêng cho test trên `127.0.0.1:5679`,
với tài khoản test khớp `IdentityMessagingTests`. Script luôn cấu hình
`SMARTPARK_RABBIT_TEST_PORT` (mặc định 5679) để test messaging được chạy.
Nếu port này đã được dùng, đặt `SMARTPARK_RABBIT_TEST_PORT` trong `.env`,
chạy lại lệnh compose và script. Khi dùng `-EnvPath` khác `.env`, truyền cùng
file đó cho `docker compose --env-file <file> -f compose.tests.yml up -d --wait`.
Sau khi test xong có thể dừng và xóa broker test cùng volume của nó bằng:

```powershell
docker compose -f compose.tests.yml down -v
```

Nếu dùng PostgreSQL test riêng, có thể đặt `SMARTPARK_AUTH_TEST_CONNECTION` và
`SMARTPARK_STRUCTURE_TEST_CONNECTION` trong `.env`; script ưu tiên các giá trị này.
Có thể dùng `-EnvPath <đường-dẫn-file-env-local>` để chọn file cấu hình khác.
Tài khoản kết nối cần quyền tạo database. Các fixture tạo DB tạm tên có GUID,
chạy test rồi xóa trong bước cleanup; không dùng bảng nghiệp vụ hiện tại để chứa dữ liệu test.
Nếu tiến trình bị kill đột ngột, cleanup có thể chưa chạy.

## Visual Studio / Test Explorer

Các test project tự nạp `smartpark.local.runsettings` qua `tests/Directory.Build.props`
khi file đã tồn tại. Sau khi tạo file lần đầu, đóng/mở lại solution để Visual Studio
nạp lại cấu hình, rồi chạy lại test trong Test Explorer.

Nếu vẫn thấy kết quả skip cũ, chọn **Test > Configure Run Settings > Select Solution Wide runsettings File**,
chọn `smartpark.local.runsettings` ở thư mục gốc repo và chạy lại test.
`.env` không tự được Test Explorer đọc; phải chạy script tạo settings trước.
Nếu test RabbitMQ còn skip, tạo lại settings bằng script mới rồi chọn lại file trong Visual Studio.

## Terminal

Smoke test cho bản sửa Approve Owner (PowerShell, từ thư mục gốc repo):

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj -c Release --settings smartpark.local.runsettings --filter "FullyQualifiedName~OwnerRegistrationTests|FullyQualifiedName~OwnerApprovalCoordinationTests|FullyQualifiedName~OwnerTenantApprovalTests|FullyQualifiedName~OwnerRegistrationHttpTests"
```

Chạy toàn bộ test .NET:

```powershell
dotnet test SmartParking.slnx -c Release --settings smartpark.local.runsettings
```

Release giúp tránh đụng file DLL Debug khi API đang chạy trong Visual Studio.
Chọn đúng `.runsettings` chỉ cấp kết nối cho test, không bảo đảm mọi test sẽ pass:
test có thể phát hiện lỗi thật sau khi hết skip. Broker test phải đang chạy khi chạy
toàn bộ suite; test messaging kiểm tra outbox PostgreSQL → RabbitMQ → inbox/projection → SignalR.
Các lệnh trên không xác nhận việc gửi email tới Gmail thật hoặc thao tác qua giao diện.
