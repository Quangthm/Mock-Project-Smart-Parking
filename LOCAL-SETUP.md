# Chạy SmartParking local trên Windows

Mở PowerShell tại đúng checkout và kiểm tra nhánh trước khi chạy:

```powershell
# Mở terminal tại root checkout vừa clone
git branch --show-current
.\setup.ps1
```

Nhánh làm việc mặc định là `develop` (kiểm tra bằng `git branch --show-current`). Cần .NET SDK 10 và Docker Desktop đang chạy (Docker Compose hỗ trợ `up --wait`). Node.js/npm cần cho frontend. Script `setup.ps1` được lưu trong Git và không chứa mật khẩu cố định. Lệnh cũ `.\scripts\setup-local.ps1` vẫn chuyển tiếp đến script này.

Setup tạo `.env` từ `.env.example`, sinh giá trị local còn thiếu cho password PostgreSQL, service key, password Admin, owner và operator. Script giữ nguyên `.env` đã cấu hình; user-secrets được đồng bộ theo `.env` mỗi lần chạy `[5/7]` (ghi đè giá trị lệch, xóa khóa optional JWT/SMTP không còn trong `.env`), không in mật khẩu ra terminal. Cấu hình `.cache/qa-local.json` cũ được giữ nguyên nhưng runner hiện tại đọc `.env`. Không commit `.env`, user-secrets hoặc thư mục workflow keys.

Setup dựng sáu PostgreSQL database riêng theo `compose.schema-integration.yml`, kiểm tra xác thực ba database chính, áp dụng workflow migration và restore solution. Không tự chạy API hoặc frontend. SonarQube được dựng tự động cùng setup, bỏ qua bằng `-SkipSonarQube` (`-SkipDatabase` chỉ dựng credential, không khởi động container).

AI camera là tùy chọn riêng: chạy `.\setup.ps1 -WithAiCamera` để build và start 2 container từ `compose.ai.yml` (project `smartpark-ai`): `ai-rest` REST API tại http://localhost:5001, `ai-live` luồng nhận diện trực tiếp tại http://localhost:5002, gắn vào mạng ngoài `smartpark-schema_default`. Không thêm flag thì không build/chạy gì thuộc AI. Flag chỉ hợp lệ khi chạy full setup (cần schema DBs đang chạy, không dùng chung với `-SkipDatabase`) và cần `SMARTPARK_DEV_PASSWORD` đã cấu hình trong `.env`. Build lần đầu ~5–10 phút (PyTorch CPU wheels + PaddleOCR warm-up), tốn RAM; các lần sau dùng cache.



Nếu chỉ muốn cấu hình và restore backend mà chưa dựng database:

```powershell
.\setup.ps1 -SkipDatabase
# Khi đã restore package trước đó, có thể bỏ qua restore:
.\setup.ps1 -SkipDatabase -SkipRestore
```

`-SkipDatabase` không tạo hay cập nhật schema. Chạy lại setup bình thường trước khi chạy API nếu database chưa sẵn sàng. Với lần cài đặt đầu tiên, mở `.env` để xem mật khẩu Admin được tạo; mặc định email là `admin@smartpark.local`. UserService còn seed thêm tài khoản owner (`owner@smartpark.local`) và operator (`operator@smartpark.local`) khi service khởi động — password tương ứng được tạo/gán trong `.env` (`SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`). UserService chỉ tạo các tài khoản này lần đầu và không thay mật khẩu đã có. Ngoài ra còn có tài khoản driver demo (`driver@gmail.com / Password@123`) do `DataSeeder` tạo trong code.

| Flag | Ý nghĩa |
| --- | --- |
| `-SkipDatabase` | Bỏ qua init DB (vẫn dựng credential) |
| `-SkipRestore` | Bỏ qua `dotnet restore` |
| `-SkipUserSecrets` | Giữ nguyên user-secrets cũ |
| `-RecreateDb` | Xóa schema volumes + init lại 6 DB |
| `-CredentialMode Team -TeamEnvPath <file>` | Dùng credential team file |
| `-SkipSonarQube` | Không khởi động SonarQube (mặc định chạy cùng setup) |
| `-NonInteractive` | Bỏ qua prompt, tự động Keep `.env` cũ |
| `-WithAiCamera` | Build + chạy 2 container AI (ai-rest :5001, ai-live :5002) |

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
| Parking | http://localhost:5045 | 5442 | smartpark_facility |
| Reservation | http://localhost:5055 | 5443 | smartpark_operations |
| Payment | — | 5444 | smartpark_payment |
| Notification | — | 5445 | smartpark_notification |
| IoT | — | 5446 | smartpark_iot |

Runner dùng `.env` cho connection strings, shared service key, Admin/owner/operator và tùy chọn SMTP/JWT/workflow keys. Khi chạy trực tiếp bằng Visual Studio, user-secrets đã được `[5/7]` đồng bộ theo `.env`; nếu tự sửa secrets sau đó, chạy lại `.\setup.ps1` (hoặc dùng runner) để đồng bộ lại. DB port đổi bằng `SMARTPARK_DB_*_PORT` trong `.env`; ba API giữ port 5035/5045/5055.

## Schema và dữ liệu có sẵn

Database mới chạy SQL baseline trong `scripts/database/microservices` (01–06 theo từng service), tiếp theo overlay trong `scripts/database/overlay` theo đúng thứ tự mount của `compose.schema-integration.yml`. Runner chỉ áp bộ file này; script legacy `05.1`–`05.9` và `integration/01–03` đã bị thay thế và bị block-list chặn. `docker-compose.yml` là compose monolith cũ (port 5433), không được setup hiện tại sử dụng.

### Seed dữ liệu demo (owner/operator + site)

Sau khi UserService đã khởi động lần đầu (owner/operator được seed), chạy:

```powershell
.\scripts\seed-demo-site.ps1
```

Script tạo tenant `DEMO-OWNER`, site `DEMO-HQ`, 1 floor, 1 zone, 8 slot (6 CAR + 2 MOTORCYCLE), operator grant và capacity pools. Chạy lại an toàn (idempotent). Nếu báo thiếu owner/operator, chạy `.\scripts\run-schema-service.ps1 -Service User` một lần rồi seed lại. Script không in password.

PostgreSQL chỉ chạy init SQL khi volume mới hoàn toàn. Với volume đã tồn tại, `scripts/apply-workflow-migrations.ps1` probe trạng thái DB trước khi áp script: schema đã đúng (sentinel có mặt) → bỏ qua và chỉ kiểm tra bảng bắt buộc; DB trống → áp trọn bộ canonical; schema cũ (có bảng cũ nhưng thiếu sentinel) → dừng và yêu cầu `.\setup.ps1 -RecreateDb`. Runner không reset volume và không chạy lại CREATE TABLE trên dữ liệu đang dùng.

### Volume cũ (thiếu bảng `structure_synchronizations`, `structure_release_receipts`, `tenants`, …)

Volume tạo trước baseline mới nằm ở trạng thái legacy — thiếu bảng overlay và runner dừng, yêu cầu dựng lại:

```powershell
.\setup.ps1 -RecreateDb
```

`-RecreateDb` xóa schema volumes và init lại cả 6 DB từ script canonical — mất dữ liệu dev trong volume đó; sao lưu trước nếu cần giữ. Volume đã init theo baseline mới không cần bước này: chạy lại `.\setup.ps1` là an toàn (migration skip và chỉ verify).

Kiểm tra bảng trên volume hiện có:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml exec -T facility-db psql -U smartpark_facility -d smartpark_facility -At -c "SELECT to_regclass('structure_synchronizations') IS NOT NULL;"
docker compose --env-file .env -f compose.schema-integration.yml exec -T operations-db psql -U smartpark_operations -d smartpark_operations -At -c "SELECT to_regclass('structure_release_receipts') IS NOT NULL;"
```

Kết quả `t` nghĩa là bảng đã có; volume legacy chưa có bảng → dùng `-RecreateDb` thay vì áp tay. Với service khác, thay `-Service` và container/database tương ứng.

Không có thao tác xóa/reset volume trong setup (ngoại trừ khi bạn chủ động chạy `-RecreateDb`). Nếu có volume cũ mà không có password local, khôi phục `SMARTPARK_DEV_PASSWORD` gốc vào `.env`. Đổi environment variable không đổi password bên trong PostgreSQL. Sao lưu database trước khi chỉnh dữ liệu hoặc schema quan trọng.

Để dừng container và giữ nguyên dữ liệu:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml down
```

Để kiểm tra SQL bootstrap khi service không lên:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml ps
docker compose --env-file .env -f compose.schema-integration.yml logs user-db facility-db operations-db
```

## Frontend và email OTP

Setup sao chép `FE/.env.example` sang `FE/.env` nếu chưa có, giữ nguyên file đã cấu hình. Điền MapTiler key của bạn nếu giao diện cần bản đồ. Sau đó:

```powershell
cd FE
npm install
npm run dev
```

Vite chạy mặc định trên http://localhost:5173. Điền `SMARTPARK_SMTP_HOST`, `PORT`, `USERNAME`, `PASSWORD`, `FROM` vào root `.env` để gửi OTP email, rồi khởi động lại UserService. Runner chuyển các giá trị này sang cấu hình `Otp:Smtp`. Không tự điền credential SMTP hoặc MapTiler của người khác.

Lỗi thường gặp: Docker Desktop chưa chạy; cổng database/API bị chiếm; password không khớp volume cũ; .NET SDK chưa đúng phiên bản; dependency chưa restore. Thông báo setup chỉ ra bước lỗi và không xóa dữ liệu để khắc phục. Khi chạy `.\setup.ps1`, dòng `[n/7]` cuối cùng trên màn hình cho biết đúng bước đang fail; bảng bước ↔ lỗi ↔ cách fix ở [QUICK_START.md](QUICK_START.md). Xem thêm [schema integration](docs/02-architecture/database/schema-integration.md) và [workflow migrations](docs/02-architecture/database/workflow-upgrades-2026-10-08.md).

## Credential Team và công cụ tùy chọn

Xem [QUICK_START.md](QUICK_START.md) để dùng `-CredentialMode Team -TeamEnvPath <file>`, `-SkipUserSecrets`, URL service xuyên máy, MapTiler và `-SkipSonarQube`. Xem [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md) để backup script local trước khi chuyển sang setup tracked. Setup không thay password của Admin đã có trong DB.
