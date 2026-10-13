# Quick start — SmartParking

Chạy PowerShell tại root của checkout vừa clone; không cần đường dẫn máy cá nhân.

```powershell
git branch --show-current
.\setup.ps1
```

Cần Windows PowerShell 5.1+, .NET SDK 10, Docker Desktop và Docker Compose hỗ trợ `up --wait`. Node.js/npm cần cho frontend. `setup.ps1` được lưu trong Git; không cần xin script từ kênh nội bộ.

Mặc định `Local`: dùng lại credential trong `.env`/user-secrets; chỉ sinh khi thiếu. Ba API dùng cùng service key trên máy này. Để các dev dùng cùng credential, lấy **file credential riêng do leader cung cấp**, rồi chạy:

```powershell
.\setup.ps1 -CredentialMode Team -TeamEnvPath '<file-env-riêng-của-team>'
```

File Team phải có `SMARTPARK_DEV_PASSWORD`, `SMARTPARK_SERVICE_KEY` (ít nhất 32 ký tự), `SMARTPARK_ADMIN_PASSWORD` (8–15 ký tự, đủ hoa/thường/số/ký tự đặc biệt). Có thể kèm `SONAR_DB_PASSWORD`, `VITE_MAPTILER_API_KEY` và `SMARTPARK_SERVICE_USER_URL`, `SMARTPARK_SERVICE_PARKING_URL`, `SMARTPARK_SERVICE_RESERVATION_URL`. File Team không thay thế cả `.env`: port, SMTP và cấu hình riêng trên máy vẫn giữ nguyên. Không commit file Team hoặc secret thật; lưu ngoài repo.

Setup dừng nếu credential Team khác cấu hình local đã có, hoặc `.env` khác credential user-secrets. Nó không tự ghi đè/reset. Nếu muốn giữ user-secrets cũ và chạy API hoàn toàn bằng `.env`, dùng `-SkipUserSecrets` rồi dùng runner, không chạy trực tiếp từ Visual Studio:

```powershell
.\setup.ps1 -SkipUserSecrets
.\scripts\run-schema-service.ps1 -Service User
# Hai terminal khác: -Service Parking và -Service Reservation
```

Nếu DB volume cũ đã có password khác, cần khôi phục password gốc; đổi `.env` không đổi password trong PostgreSQL. Admin đã seed cũng không được đổi password chỉ bằng setup.

SonarQube bật bằng `-WithSonarQube` sau khi cấu hình `SONAR_DB_PASSWORD`; UI mặc định http://localhost:9000. Compose Sonar riêng giữ dữ liệu trong named volumes; không tự tiếp quản container Sonar cũ. Cấu hình dựa trên [ví dụ chính thức SonarSource](https://github.com/SonarSource/docker-sonarqube/blob/master/example-compose-files/sq-with-postgres/docker-compose.yml). Pin `SONAR_IMAGE` theo version team duyệt nếu cần môi trường cố định. Lần đầu đăng nhập admin/admin và đổi password trên UI.

MapTiler: điền `VITE_MAPTILER_API_KEY` trong root `.env` hoặc file Team; setup chỉ bổ sung nếu key trong `FE/.env` còn trống. Key FE đã có được giữ nguyên. Để chạy frontend:

```powershell
cd FE
npm install
npm run dev
```

Port mặc định: User/Parking/Reservation API 5035/5045/5055; User/Parking/Reservation DB 5441/5442/5443; Payment/Notification/IoT DB 5444/5445/5446; FE 5173. DB bind loopback. Giao tiếp API xuyên máy cần URL host truy cập được, listener/firewall phù hợp và cùng service key; setup không tự mở mạng.

Gặp warning `relation "structure_synchronizations" does not exist` (hoặc bảng khác bị thiếu) trên volume cũ: áp migration bằng `.\scripts\apply-workflow-migrations.ps1 -Service Parking -SchemaRoot .`; chi tiết ở [LOCAL-SETUP.md](LOCAL-SETUP.md).

Chi tiết về volume, migrations và chuyển từ script cũ: [LOCAL-SETUP.md](LOCAL-SETUP.md). Hướng dẫn sửa script: [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md).
