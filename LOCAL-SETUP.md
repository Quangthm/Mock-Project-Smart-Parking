# Chạy SmartParking local trên Windows

Mở PowerShell tại đúng checkout và kiểm tra nhánh trước khi chạy:

```powershell
# Mở terminal tại root checkout vừa clone
git branch --show-current
.\setup.ps1
```

Nhánh làm việc là `develop-fix-application`. Cần .NET SDK 10 và Docker Desktop đang chạy (Docker Compose hỗ trợ `up --wait`). Node.js/npm cần cho frontend. Script `setup.ps1` được lưu trong Git và không chứa mật khẩu cố định. Lệnh cũ `.\scripts\setup-local.ps1` vẫn chuyển tiếp đến script này.

Setup tạo `.env` từ `.env.example`, sinh giá trị local còn thiếu cho password PostgreSQL, service key và password Admin. Nếu tìm thấy thông tin tương ứng trong user-secrets, script dùng lại chúng. Script giữ nguyên `.env` đã cấu hình và mọi khóa user-secrets đang có; chỉ bổ sung khóa thiếu, không in mật khẩu ra terminal. Cấu hình `.cache/qa-local.json` cũ được giữ nguyên nhưng runner hiện tại đọc `.env`. Không commit `.env`, user-secrets hoặc thư mục workflow keys.

Setup dựng sáu PostgreSQL database riêng theo `compose.schema-integration.yml`, kiểm tra xác thực ba database chính, áp dụng workflow migration và restore solution. Không tự chạy API hoặc frontend. SonarQube chỉ được dựng khi thêm `-WithSonarQube`. Nếu chỉ muốn cấu hình và restore backend mà chưa dựng database:

```powershell
.\setup.ps1 -SkipDatabase
# Khi đã restore package trước đó, có thể bỏ qua restore:
.\setup.ps1 -SkipDatabase -SkipRestore
```

`-SkipDatabase` không tạo hay cập nhật schema. Chạy lại setup bình thường trước khi chạy API nếu database chưa sẵn sàng. Với lần cài đặt đầu tiên, mở `.env` để xem mật khẩu Admin được tạo; mặc định email là `admin@smartpark.local`. UserService chỉ tạo Admin khi service khởi động và không thay mật khẩu của Admin đã có.

## Chạy backend

Mở ba terminal PowerShell riêng tại repo:

```powershell
.\scripts\run-schema-service.ps1 -Service User
```

```powershell
.\scripts\run-schema-service.ps1 -Service Parking
```

```powershell
.\scripts\run-schema-service.ps1 -Service Reservation
```

| Service | HTTP API | PostgreSQL host port | Database |
| --- | --- | --- | --- |
| User | http://localhost:5035 | 5441 | smartpark_user |
| Parking | http://localhost:5045 | 5442 | smartpark_parking |
| Reservation | http://localhost:5055 | 5443 | smartpark_reservation |
| Payment | — | 5444 | smartpark_payment |
| Notification | — | 5445 | smartpark_notification |
| IoT | — | 5446 | smartpark_iot |

Runner dùng `.env` cho connection strings, shared service key, Admin và tùy chọn SMTP/JWT/workflow keys. Khi chạy trực tiếp bằng Visual Studio, user-secrets có sẵn vẫn được giữ nguyên; nếu chúng khác `.env`, hãy đồng bộ chủ động hoặc dùng runner để có đúng cấu hình local. DB port đổi bằng `SMARTPARK_DB_*_PORT` trong `.env`; ba API giữ port 5035/5045/5055.

## Schema và dữ liệu có sẵn

Database mới chạy SQL baseline trong `scripts/database/microservices`, tiếp theo overlay trong `integration`, rồi workflow `05.8`, `05.9`, `05.10`, đồng bộ cấu trúc `05.11` và receipts `05.12` và vòng đời 2FA `05.13` theo từng service. Các migration lịch sử `05.2`–`05.7` thuộc đường nâng cấp monolith, không chạy lên database service mới. `docker-compose.yml` là compose monolith cũ (port 5433), không được setup hiện tại sử dụng.

PostgreSQL chỉ chạy init SQL khi volume mới hoàn toàn. Với volume đã tồn tại, setup chạy lại workflow migration có thể lặp lại an toàn, qua script `scripts/apply-workflow-migrations.ps1` đang có. Nếu baseline hoặc overlay bị thiếu, script dừng và yêu cầu kiểm tra schema; không chạy lại CREATE TABLE trên dữ liệu đang dùng. Migration báo contact trùng thì cần xử lý dữ liệu trước khi thử lại.

### Lỗi thiếu bảng trên volume đã tồn tại (ví dụ `structure_synchronizations`, `structure_release_receipts`)

Vì PostgreSQL chỉ chạy init SQL khi volume mới hoàn toàn, một volume tạo trước khi migration được thêm vào `compose.schema-integration.yml` sẽ thiếu bảng của migration đó. Đây là lỗi trạng thái database, không phải lỗi code.

ParkingService log warning lặp mỗi 10 giây:

```text
warn: SmartParking.ParkingService.Infrastructure.StructureSynchronizationWorker[0] Structure synchronization failed and will be retried. Npgsql.PostgresException (0x80004005): 42P01: relation "structure_synchronizations" does not exist
```

ReservationService trả HTTP 500 khi load parking structure:

```text
Npgsql.PostgresException (0x80004005): 42P01: relation "structure_release_receipts" does not exist
   at SmartParking.ReservationService.Persistence.StructureSafetyStore.AcquireAsync(...)
```

Áp migration lên volume hiện có (idempotent, không mất dữ liệu):

```powershell
.\scripts\apply-workflow-migrations.ps1 -Service Parking -SchemaRoot .
.\scripts\apply-workflow-migrations.ps1 -Service Reservation -SchemaRoot .
```

Script kiểm tra bảng nền tảng bắt buộc trước, rồi áp `05.9` + `05.11` (Parking) hoặc `05.10` + `05.12` (Reservation); các câu lệnh dùng `CREATE TABLE IF NOT EXISTS` nên chạy lại an toàn. Kiểm tra lại:

```powershell
docker compose -f compose.schema-integration.yml exec -T parking-db psql -U smartpark_parking -d smartpark_parking -At -c "SELECT to_regclass('structure_synchronizations') IS NOT NULL;"
docker compose -f compose.schema-integration.yml exec -T reservation-db psql -U smartpark_reservation -d smartpark_reservation -At -c "SELECT to_regclass('structure_release_receipts') IS NOT NULL;"
```

Kết quả `t` nghĩa là bảng đã có; warning/HTTP 500 tự dừng. Cách khác: chạy lại `.\setup.ps1` không kèm `-SkipDatabase`. Không cần xóa volume. Với service khác, thay `-Service` và container/database tương ứng.

Không có thao tác xóa/reset volume trong setup. Nếu có volume cũ mà không có password local, khôi phục `SMARTPARK_DEV_PASSWORD` gốc vào `.env`. Đổi environment variable không đổi password bên trong PostgreSQL. Sao lưu database trước khi chỉnh dữ liệu hoặc schema quan trọng.

Để dừng container và giữ nguyên dữ liệu:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml down
```

Để kiểm tra SQL bootstrap khi service không lên:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml ps
docker compose --env-file .env -f compose.schema-integration.yml logs user-db parking-db reservation-db
```

## Frontend và email OTP

Setup sao chép `FE/.env.example` sang `FE/.env` nếu chưa có, giữ nguyên file đã cấu hình. Điền MapTiler key của bạn nếu giao diện cần bản đồ. Sau đó:

```powershell
cd FE
npm install
npm run dev
```

Vite chạy mặc định trên http://localhost:5173. Điền `SMARTPARK_SMTP_HOST`, `PORT`, `USERNAME`, `PASSWORD`, `FROM` vào root `.env` để gửi OTP email, rồi khởi động lại UserService. Runner chuyển các giá trị này sang cấu hình `Otp:Smtp`. Không tự điền credential SMTP hoặc MapTiler của người khác.

Lỗi thường gặp: Docker Desktop chưa chạy; cổng database/API bị chiếm; password không khớp volume cũ; .NET SDK chưa đúng phiên bản; dependency chưa restore. Thông báo setup chỉ ra bước lỗi và không xóa dữ liệu để khắc phục. Xem thêm [schema integration](docs/02-architecture/database/schema-integration.md) và [workflow migrations](docs/02-architecture/database/workflow-upgrades-2026-10-08.md).

## Credential Team và công cụ tùy chọn

Xem [QUICK_START.md](QUICK_START.md) để dùng `-CredentialMode Team -TeamEnvPath <file>`, `-SkipUserSecrets`, URL service xuyên máy, MapTiler và `-WithSonarQube`. Xem [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md) để backup script local trước khi chuyển sang setup tracked. Setup không thay password của Admin đã có trong DB.
