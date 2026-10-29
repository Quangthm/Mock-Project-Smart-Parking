# Chạy SmartParking local trên Windows

Tài liệu onboarding chi tiết cho môi trường local. Happy path nằm ở [TL;DR](#tldr-happy-path) và các mục 1–4; phần nâng cao (volume, migration, AI camera, Team credential) ở mục 5–7. Không sửa `setup.ps1`/script khi chỉ muốn chạy local.

## TL;DR happy path

```powershell
# Mở terminal tại root checkout vừa clone
git branch --show-current          # nhánh làm việc mặc định: develop
.\setup.ps1                        # dựng DB + credential + SonarQube + restore
.\scripts\run-app-service.ps1 -Service All   # chạy 5 container BE + FE
# Mở http://localhost:5173
```

## 1. Prerequisites

Checklist trước khi setup:

- [ ] **Windows PowerShell 5.1+**, **.NET SDK 10** (`dotnet --version` trả major 10).
- [ ] **Docker Desktop đang chạy**, Docker Compose hỗ trợ `up --wait` (v2.17+).
- [ ] **Node.js + npm** nếu chạy frontend trực tiếp trên host. Docker app stack dùng Node 22 trong image, không cần cài Node trên host.
- [ ] **Git** và đúng branch: `git branch --show-current` (mặc định `develop`).
- [ ] **Port rảnh:** API `5035/5045/5055/5065`, FE `5173`, SonarQube `9000`, DB `5441–5446`, AI (tùy chọn) `5001/5002`.

`setup.ps1` được lưu trong Git và **không chứa mật khẩu cố định**. Lệnh cũ `.\scripts\setup-local.ps1` vẫn dùng được: script shim này in warning rồi forward toàn bộ tham số sang `setup.ps1` ở repo root.

### Thuật ngữ nhanh

| Thuật ngữ | Nghĩa ngắn gọn |
| --- | --- |
| **Schema volume** | Named volume Docker giữ dữ liệu PostgreSQL. Xóa volume = mất dữ liệu dev. |
| **Runner** | `scripts/run-schema-service.ps1` chạy một API với biến môi trường nạp từ `.env`. |
| **user-secrets** | Kho secret .NET cho Visual Studio/`dotnet run`; setup đồng bộ từ `.env` ở `[5/7]`. |
| **Canonical script** | SQL baseline trong `scripts/database/microservices` + overlay, được mount theo thứ tự của compose. |
| **Sentinel** | Bảng/cột đánh dấu schema đã đúng để runner biết có cần áp migration hay không. |
| **Block-list** | Danh sách script legacy bị runner chặn, không được áp lại. |

## 2. First-time Setup

```powershell
.\setup.ps1
```

`setup.ps1` làm các việc sau:

1. Tạo `.env` từ `.env.example`; **giữ nguyên** giá trị đã cấu hình, chỉ sinh phần còn thiếu (password PostgreSQL, service key, password Admin/owner/operator).
2. Dựng **6 PostgreSQL DB** theo `compose.schema-integration.yml` (`user`, `facility`, `operations`, `payment`, `notification`, `iot`).
3. Probe authentication từng DB và áp workflow migration đúng trạng thái schema.
4. Đồng bộ user-secrets cho User/Facility/Operation/Payment theo `.env` ở `[5/7]`.
5. Dựng SonarQube (mặc định) và restore solution ở `[7/7]`.
6. **Không tự chạy API hoặc frontend** — sau setup mới chuyển sang [mục 3](#3-run-application).

### Cảnh báo ghi đè và mất dữ liệu

Nếu `.env` đã tồn tại, setup hỏi `[O]verwrite / [K]eep / [A]bort` (**Enter = Keep**).

> **CẢNH BÁO — ghi đè cấu hình:** chọn `O` sẽ backup `.env` hiện tại ra `.env.bak` rồi sinh lại `.env` từ `.env.example`. Password/key đang dùng có thể bị thay; chỉ chọn `O` khi chấp nhận cấu hình lại. `-NonInteractive` tự động Keep.

> **CẢNH BÁO — ghi đè user-secrets:** `[5/7]` ghi đè giá trị user-secrets lệch theo `.env` và xóa khóa optional (JWT PEM, SMTP) không còn trong `.env` mỗi lần chạy setup. Muốn giữ user-secrets cũ: dùng `-SkipUserSecrets` + runner, **không** chạy trực tiếp từ Visual Studio.

> **CẢNH BÁO — mất dữ liệu:** **không chạy `-RecreateDb` ở lần setup đầu.** Flag này xóa schema volume và init lại 6 DB, làm mất dữ liệu dev. Chi tiết ở mục 7.3.

Cấu hình `.cache/qa-local.json` cũ **được giữ nguyên**, nhưng runner hiện tại đọc `.env` (`import-local-config.ps1` chỉ còn là tiện ích cho config dạng flat string). Không commit `.env`, user-secrets hoặc thư mục workflow keys.

### Chỉ cấu hình backend, chưa dựng database

```powershell
.\setup.ps1 -SkipDatabase
# Khi đã restore package trước đó, có thể bỏ qua restore:
.\setup.ps1 -SkipDatabase -SkipRestore
```

`-SkipDatabase` không tạo hay cập nhật schema. Chạy lại setup bình thường trước khi chạy API nếu database chưa sẵn sàng.

### Tài khoản được seed

- Admin: xem `SMARTPARK_ADMIN_PASSWORD` trong `.env`; email mặc định `admin@smartpark.local`.
- Owner/operator: UserService seed thêm `owner@smartpark.local` và `operator@smartpark.local` khi service khởi động; password từ `.env` (`SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`).
- UserService **chỉ tạo lần đầu** và không thay mật khẩu tài khoản đã có.
- Driver demo: `driver@gmail.com / Password@123` do `DataSeeder` tạo trong code.

## 3. Run Application

Chọn **một** trong hai cách; không chạy đồng thời vì cùng dùng port `5035/5045/5055/5065`.

### 3.1 Docker app stack (BE + FE)

Yêu cầu `setup.ps1` đã chạy xong (6 DB healthy và network `smartpark-schema_default` tồn tại). Không cần cài .NET/Node trên host sau lần build đầu.

```powershell
.\scripts\run-app-service.ps1 -Service User
.\scripts\run-app-service.ps1 -Service Facility
.\scripts\run-app-service.ps1 -Service Operation
.\scripts\run-app-service.ps1 -Service Payment
.\scripts\run-app-service.ps1 -Service Frontend
.\scripts\run-app-service.ps1 -Service All        # cả 5 container
```

| Container | Host URL | Ghi chú |
| --- | --- | --- |
| `smartpark-app-user-service-1` | http://localhost:5035 | `/health`; seed admin/owner/operator khi start |
| `smartpark-app-parking-service-1` | http://localhost:5045 | `/health`; dùng `facility-db` |
| `smartpark-app-reservation-service-1` | http://localhost:5055 | `/health`; dùng `operations-db` |
| `smartpark-app-payment-service-1` | http://localhost:5065 | `/health`; dùng `payment-db`; PayOS rỗng = sandbox emulation |
| `smartpark-app-frontend-1` | http://localhost:5173 | Vite dev + HMR, bind mount `FE/` |

> **Lưu ý chuyển tiếp v1.1 / Fix2FA:** Mã nguồn trên branch `Fix2FA` đã dùng `FacilityService` tại `src/Services/FacilityService/` (dùng `facility-db` trên port 5442) theo SRS v1.1 (quyết định V11-09). `OperationService` tại `src/Services/OperationService/` dùng `operations-db` trên port 5443 và triển khai luồng Parking Operations. Các port và contract API vẫn giữ nguyên tính tương thích.

- Mỗi lệnh build image nếu cần, chạy đúng một service và mở một cửa sổ CMD theo dõi log.
- `-NoBuild` chạy nhanh khi image đã có; `-NoLogWindow` cho CI/automation.
- Double-click được: `scripts\run-*.cmd` hoặc `scripts\run-all-apps.cmd`.
- Dừng stack (giữ volume): `.\scripts\stop-app-service.ps1 -Service All`.
- Log một service: `docker compose --project-name smartpark-app --env-file .env -f compose.app.yml logs --tail 200 <service>`.

Compose file là `compose.app.yml`; image backend dùng `docker/Dockerfile.backend` với build arg `SERVICE` (`UserService`/`FacilityService`/`OperationService`/`PaymentService`).

> **CẢNH BÁO:** không đặt `VITE_MAPTILER_API_KEY` rỗng trong Compose — biến rỗng sẽ ghi đè key trong `FE/.env` đang bind mount. Nếu port `5035/5045/5055/5065` đang bị runner host chiếm, dừng runner host trước khi chạy container.

### 3.2 Runner trên host (Visual Studio / `dotnet run`)

Mở bốn terminal PowerShell riêng tại repo:

```powershell
.\scripts\run-schema-service.ps1 -Service User
```

```powershell
.\scripts\run-schema-service.ps1 -Service Facility
```

```powershell
.\scripts\run-schema-service.ps1 -Service Operation
```

```powershell
.\scripts\run-schema-service.ps1 -Service Payment
```

Runner dùng `.env` cho connection string, shared service key, Admin/owner/operator và tùy chọn SMTP/JWT/workflow keys. Khi chạy trực tiếp bằng Visual Studio, user-secrets đã được `[5/7]` đồng bộ theo `.env`; nếu tự sửa secrets sau đó, chạy lại `.\setup.ps1` (hoặc dùng runner) để đồng bộ lại. DB port đổi bằng `SMARTPARK_DB_*_PORT` trong `.env`; các API giữ port `5035/5045/5055/5065`.

| Service | HTTP API | PostgreSQL host port | Database |
| --- | --- | --- | --- |
| User | http://localhost:5035 | 5441 | smartpark_user |
| Facility | http://localhost:5045 | 5442 | smartpark_facility |
| Operation | http://localhost:5055 | 5443 | smartpark_operations |
| Payment | http://localhost:5065 | 5444 | smartpark_payment |
| Notification | — | 5445 | smartpark_notification |
| IoT | — | 5446 | smartpark_iot |

`FacilityService` là tên mới của `ParkingService`. Runner vẫn nhận `-Service Parking` để tương thích lệnh cũ. Giữ nguyên các khóa `ConnectionStrings:Parking`, `Services:Parking`, biến `SMARTPARK_*PARKING*` và UserSecretsId cũ. Database theo baseline develop là `smartpark_facility` / `smartpark_operations`; API routes và port không đổi.

### 3.3 Frontend trên host

```powershell
cd FE
npm install
npm run dev
```

Vite chạy mặc định tại http://localhost:5173. Setup sao chép `FE/.env.example` sang `FE/.env` nếu chưa có và **giữ nguyên file đã cấu hình**. Điền MapTiler key của bạn nếu giao diện cần bản đồ (xem mục 5.2).

## 4. Verify Setup

Checklist sau khi setup và chạy app:

- [ ] **6 DB container chạy:**

  ```powershell
  docker compose --env-file .env -f compose.schema-integration.yml ps
  ```

- [ ] **4 API health 200** (khi runner/container đã chạy):

  ```powershell
  Invoke-WebRequest http://localhost:5035/health -UseBasicParsing
  Invoke-WebRequest http://localhost:5045/health -UseBasicParsing
  Invoke-WebRequest http://localhost:5055/health -UseBasicParsing
  Invoke-WebRequest http://localhost:5065/health -UseBasicParsing
  ```

- [ ] **FE mở được** tại http://localhost:5173.
- [ ] **Đăng nhập được admin/owner/operator** (password trong `.env`) và driver demo `driver@gmail.com / Password@123`.
- [ ] **Seed demo site** sau khi UserService đã khởi động lần đầu:

  ```powershell
  .\scripts\seed-demo-site.ps1
  ```

  Script tạo tenant `DEMO-OWNER`, site `DEMO-HQ`, 1 floor, 1 zone, 8 slot (6 `CAR` + 2 `MOTORCYCLE`), operator grant và capacity pools. Chạy lại an toàn (idempotent). Nếu báo thiếu owner/operator, chạy `.\scripts\run-schema-service.ps1 -Service User` một lần rồi seed lại. Script không in password.
- [ ] **SonarQube** mở được tại http://localhost:9000 (xem [mục 5.3](#53-sonarqube)).

## 5. Optional Features

### 5.1 AI camera

`.\setup.ps1 -WithAiCamera` kiểm tra 7 model file trong `src/Services/IoTService/python-ai/models` và network `smartpark-schema_default`, rồi dựng 2 container từ `compose.ai.yml` (project `smartpark-ai`): `ai-rest` tại http://localhost:5001, `ai-live` tại http://localhost:5002. **Không thêm flag thì không build/chạy gì thuộc AI.** Flag chỉ hợp lệ khi chạy full setup (cần schema DBs đang chạy, không dùng chung `-SkipDatabase`) và cần `SMARTPARK_DEV_PASSWORD` trong `.env`.

Setup chỉ build khi cần:

| Trạng thái | Hành vi |
| --- | --- |
| Image `smartpark-ai:local` chưa có | Build + `up -d --wait` (lần đầu ~5–10 phút do PyTorch CPU wheels + PaddleOCR warm-up, tốn RAM) |
| Image có, chưa có container | In playbook A–E, probe thư viện một lần, hỏi `[S] Start / [R] Rebuild / [C] Clean rebuild / [K] Skip AI / [A] Abort`; `-NonInteractive` mặc định Start |
| Image + container đã có | Bỏ qua build, `up -d --wait --no-build` |
| Container dùng image ID cũ | Cảnh báo, recreate bằng `--force-recreate --no-build`, không tự build |
| Container unhealthy | Không tự rebuild, in `logs --tail 200` và hướng dẫn |

`-RebuildAi` chỉ dùng cùng `-WithAiCamera` để buộc build lại có cache. "Skip" chỉ bỏ build; setup vẫn gọi `up --no-build` để đảm bảo container chạy (chọn `K` mới bỏ toàn bộ AI). Nếu probe thư viện fail, setup cảnh báo và không tự rebuild; chọn `R`/`C` hoặc chạy lại với `-RebuildAi` sau khi xác nhận. Thiếu model file thì setup dừng trước khi build/chạy AI và liệt kê file thiếu; thiếu network `smartpark-schema_default` thì chạy setup không `-SkipDatabase` trước.

Kiểm tra nhanh:

```powershell
docker image inspect smartpark-ai:local --format '{{.Id}}|{{.Created}}|{{.Size}}'
docker compose -p smartpark-ai --env-file .env -f compose.ai.yml ps --all
docker compose -p smartpark-ai --env-file .env -f compose.ai.yml logs --tail 200
docker run --rm --entrypoint python smartpark-ai:local -c "import cv2,numpy,scipy,torch,paddleocr,ultralytics,psycopg,flask; print('AI_IMAGE_OK')"
```

### 5.2 Frontend `.env`, email OTP và MapTiler

- Setup sao chép `FE/.env.example` sang `FE/.env` nếu chưa có; giá trị đã cấu hình được giữ nguyên, chỉ điền khi trống.
- Điền `VITE_MAPTILER_API_KEY` trong root `.env` hoặc file Team, hoặc trực tiếp trong `FE/.env`.
- Bật OTP email: điền `SMARTPARK_SMTP_HOST`, `SMARTPARK_SMTP_PORT`, `SMARTPARK_SMTP_USERNAME`, `SMARTPARK_SMTP_PASSWORD`, `SMARTPARK_SMTP_FROM` vào root `.env`, rồi khởi động lại UserService. Runner host và container app stack (`compose.app.yml`) đều chuyển các giá trị này sang cấu hình `Otp:Smtp`; với container, chạy lại `run-app-service.ps1 -Service User -NoBuild` để nạp env mới.
- JWT signing key (tùy chọn): nếu điền `SMARTPARK_JWT_PRIVATE_KEY_PEM` trong root `.env`, runner host và container app stack đều chuyển sang `Jwt:PrivateKeyPem`; để trống thì môi trường Development dùng ephemeral key (token do container phát mất hiệu lực khi restart). Chạy lại `run-app-service.ps1 -Service User -NoBuild` sau khi đổi.
- Không tự điền credential SMTP hoặc MapTiler của người khác.

### 5.3 SonarQube

SonarQube được dựng tự động cùng setup; bỏ qua bằng `-SkipSonarQube`. `-SkipDatabase` chỉ dựng credential, không khởi động container. UI mặc định http://localhost:9000 — lần đầu đăng nhập `admin/admin` và đổi password trên UI. `SONAR_DB_PASSWORD` được sinh khi thiếu và giữ nguyên giá trị đã cấu hình. Compose riêng `compose.sonar.yml` giữ dữ liệu trong named volumes; setup không tự tiếp quản container Sonar cũ.

### 5.4 Credential Team và URL xuyên máy

Xem [QUICK_START.md](QUICK_START.md) để dùng `-CredentialMode Team -TeamEnvPath <file>`, `-SkipUserSecrets`, URL service xuyên máy (`SMARTPARK_SERVICE_*_URL`) và MapTiler. Xem [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md) để backup script local trước khi chuyển sang setup tracked. Setup **không thay password Admin đã có trong DB**.

## 6. Troubleshooting

### 6.1 Lỗi thường gặp — Triệu chứng → Nguyên nhân → Cách xử lý

**Setup dừng ngay ở `[1/7]`/Docker command lỗi**

- **Nguyên nhân:** Docker Desktop chưa chạy hoặc Compose không hỗ trợ `up --wait`.
- **Cách xử lý:** mở Docker Desktop, chờ engine ready; nâng Docker Desktop/Compose nếu `up` báo thiếu `--wait`.

**Lỗi `Invalid port` ở `[3/7]` hoặc DB/API không lên**

- **Nguyên nhân:** port `5441–5446`, `5035/5045/5055/5065` bị chiếm.
- **Cách xử lý:** kiểm tra process đang giữ port; đổi port DB bằng `SMARTPARK_DB_*_PORT` trong `.env` (các API giữ `5035/5045/5055/5065`).

**`authentication failed` khi probe DB hoặc chạy API**

- **Nguyên nhân:** password trong `.env` khác password volume PostgreSQL đã init. Đổi environment variable **không** đổi password bên trong PostgreSQL.
- **Cách xử lý:** khôi phục `SMARTPARK_DEV_PASSWORD` gốc vào `.env`; nếu không còn password và chấp nhận mất data dev thì dùng `.\setup.ps1 -RecreateDb`.

**Chạy Visual Studio/`dotnet run` báo 28P01, runner không bị**

- **Nguyên nhân:** user-secrets còn connection string password cũ.
- **Cách xử lý:** chạy lại `.\setup.ps1` để `[5/7]` đồng bộ lại user-secrets, hoặc dùng runner.

**`dotnet restore` fail ở `[7/7]`**

- **Nguyên nhân:** mạng/proxy NuGet hoặc .NET SDK sai major.
- **Cách xử lý:** kiểm tra `dotnet --version` phải là major 10 và kết nối NuGet; có thể bỏ qua bằng `-SkipRestore` nếu đã restore trước đó.

**Service không lên, nghi lỗi SQL bootstrap**

- **Cách xử lý:** xem trạng thái và log DB:

  ```powershell
  docker compose --env-file .env -f compose.schema-integration.yml ps
  docker compose --env-file .env -f compose.schema-integration.yml logs user-db facility-db operations-db
  ```

### 6.2 Xác định bước lỗi bằng progress line

Khi chạy `.\setup.ps1`, dòng `[n/7]` cuối cùng trên màn hình cho biết đúng bước đang fail; bảng bước ↔ lỗi ↔ cách fix ở [QUICK_START.md](QUICK_START.md). Thông báo setup chỉ ra bước lỗi và **không xóa dữ liệu** để khắc phục.

## 7. Advanced Options

### 7.1 Flags của `setup.ps1`

| Flag | Ý nghĩa |
| --- | --- |
| `-SkipDatabase` | Bỏ qua init DB (vẫn dựng credential) |
| `-SkipRestore` | Bỏ qua `dotnet restore` |
| `-SkipUserSecrets` | Giữ nguyên user-secrets cũ |
| `-RecreateDb` | **Phá hủy:** xóa schema volumes + init lại 6 DB (mất dữ liệu dev) |
| `-CredentialMode Team -TeamEnvPath <file>` | Dùng credential team file |
| `-SkipSonarQube` | Không khởi động SonarQube (mặc định chạy cùng setup) |
| `-NonInteractive` | Bỏ qua prompt, tự động Keep `.env` cũ |
| `-WithAiCamera` | Kiểm tra model/network, dựng 2 container AI (`ai-rest` :5001, `ai-live` :5002); chỉ build khi image thiếu hoặc có `-RebuildAi` |
| `-RebuildAi` | Cùng `-WithAiCamera`: buộc build lại image AI có cache rồi start |
| `-WithSonarQube` | **Deprecated:** SonarQube đã chạy mặc định; flag vẫn được nhận nhưng không còn tác dụng |

> **CẢNH BÁO — trước khi chạy `-RecreateDb`:** flag này xóa schema volume và làm mất toàn bộ dữ liệu dev trong 6 DB. Sao lưu trước nếu cần giữ dữ liệu.

### 7.2 Schema baseline, overlay và script legacy

- Database mới chạy SQL baseline trong `scripts/database/microservices` (`01`–`06` theo từng service), tiếp theo overlay trong `scripts/database/overlay` theo đúng thứ tự mount của `compose.schema-integration.yml`.
- Runner chỉ áp bộ file canonical này. Script legacy `05.1`–`05.9` và `integration/01–03` đã bị thay thế và bị block-list chặn bởi `apply-workflow-migrations.ps1`.
- `docker-compose.yml` là compose monolith cũ (port `5433`, container `smartparking_db`), **không** được setup hiện tại sử dụng.

### 7.3 Volume và migration

PostgreSQL chỉ chạy init SQL khi volume **mới hoàn toàn**. Với volume đã tồn tại, `scripts/apply-workflow-migrations.ps1` probe trạng thái DB trước khi áp script:

| Trạng thái | Hành vi |
| --- | --- |
| Schema đã đúng (sentinel có mặt) | Bỏ qua, chỉ kiểm tra bảng bắt buộc |
| DB trống | Áp trọn bộ canonical |
| Schema cũ (có bảng cũ nhưng thiếu sentinel) | Dừng và yêu cầu `.\setup.ps1 -RecreateDb` |

Runner **không reset volume** và không chạy lại `CREATE TABLE` trên dữ liệu đang dùng. Volume đã init theo baseline mới chạy lại `.\setup.ps1` là an toàn (migration skip và chỉ verify).

#### Volume cũ thiếu bảng overlay

Volume tạo trước baseline mới ở trạng thái legacy — thiếu các bảng như `structure_synchronizations`, `structure_release_receipts`, `tenants`, … Runner sẽ dừng và yêu cầu dựng lại:

> **CẢNH BÁO — mất dữ liệu:** lệnh dưới đây xóa schema volumes và init lại cả 6 DB từ script canonical. Toàn bộ dữ liệu dev trong volume đó sẽ mất; sao lưu trước nếu cần giữ.

```powershell
.\setup.ps1 -RecreateDb
```

Kiểm tra bảng trên volume hiện có:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml exec -T facility-db psql -U smartpark_facility -d smartpark_facility -At -c "SELECT to_regclass('structure_synchronizations') IS NOT NULL;"
docker compose --env-file .env -f compose.schema-integration.yml exec -T operations-db psql -U smartpark_operations -d smartpark_operations -At -c "SELECT to_regclass('structure_release_receipts') IS NOT NULL;"
```

Kết quả `t` nghĩa là bảng đã có; volume legacy chưa có bảng → dùng `-RecreateDb` thay vì áp tay. Với service khác, thay container/database tương ứng.

Không có thao tác xóa/reset volume trong setup (ngoại trừ khi bạn chủ động chạy `-RecreateDb`). Nếu có volume cũ mà không có password local, khôi phục `SMARTPARK_DEV_PASSWORD` gốc vào `.env`. Đổi environment variable không đổi password bên trong PostgreSQL. Sao lưu database trước khi chỉnh dữ liệu hoặc schema quan trọng.

Để dừng container và **giữ nguyên dữ liệu** (`down` không xóa volume):

```powershell
docker compose --env-file .env -f compose.schema-integration.yml down
```

### 7.4 Cập nhật `.env` — chạy lại script gì

Đổi giá trị trong `.env` không cần chạy full setup; chạy đúng đích theo bảng:

| Thay đổi | Chạy lại | Ghi chú |
| --- | --- | --- |
| Giá trị container dùng (`SMARTPARK_SERVICE_KEY`, password admin/owner/operator) | `.\scripts\run-app-service.ps1 -Service <X\|All> -NoBuild` | Container chỉ đọc env lúc tạo; Compose tự recreate service đổi config — không cần build image |
| Giá trị runner host dùng (mọi key) | Restart `.\scripts\run-schema-service.ps1 -Service X` | Runner đọc `.env` mỗi lần start |
| Visual Studio/`dotnet run` (user-secrets) | `.\setup.ps1 -SkipDatabase -SkipRestore` | Bước `[5/7]` ghi đè user-secrets lệch theo `.env` |
| Port DB `SMARTPARK_DB_*_PORT` | `.\setup.ps1 -SkipRestore` rồi restart runner | Recreate container DB với mapping port mới |
| MapTiler / AI endpoint cho FE | Sửa trực tiếp `FE/.env`, restart Vite | Setup không ghi đè `FE/.env` đã có giá trị |
| `VITE_*` FE container / port trong compose | Sửa `compose.app.yml` rồi `run-app-service.ps1 -NoBuild` | Root `.env` không được container đọc |

> **Ngoại lệ — password gắn với volume:** `SMARTPARK_DEV_PASSWORD` và password tài khoản seed chỉ áp dụng khi DB/seed được tạo lần đầu. Đổi sau đó trong `.env` không đổi giá trị trong PostgreSQL: khôi phục giá trị gốc, dùng `.\setup.ps1 -RecreateDb` (mất dữ liệu dev), hoặc `ALTER ROLE`/sửa trong DB. Container sẽ fail health; setup báo `authentication failed`.

Không chạy full `setup.ps1` cho các trường hợp trên; full setup chỉ cần khi: volume mới, `-RecreateDb`, hoặc sau khi pull code cập nhật script. Luôn start container qua `run-app-service.ps1`/`.cmd` — Docker Desktop Start / `docker start` dùng lại image và env cũ.

### 7.5 Thêm key mới vào `.env.example` và `.env`

Checklist đầy đủ ở [SmartParking_Env_Config_SKILL_v1.0.md](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md); tóm tắt:

1. Thêm key vào `.env.example` (commit) với placeholder — không chứa secret thật.
2. Thêm giá trị thật vào `.env` local — setup **không** tự copy key mới vào `.env` đã tồn tại.
3. Wire theo consumer:
   - BE container: `environment:` trong `compose.app.yml` dạng `NewKey: ${NEW_KEY:?...}`.
   - BE host runner: tên theo chuẩn ASP.NET `Section__Key` dùng được ngay (runner export toàn bộ `.env`); tên `SMARTPARK_*` cần thêm mapping trong `run-schema-service.ps1`.
   - VS/user-secrets: thêm vào `$desired` bước `[5/7]` của `setup.ps1`; key optional thêm cả vào `$removable`.
   - Sinh/validate tự động: bổ sung bước `[2/7]`/`[3/7]`.
   - FE host: `FE/.env.example` (+ bước `[6/7]` nếu muốn auto-fill); FE container: `environment:` service `frontend` trong compose.
4. Kiểm chứng: PS 5.1 parse, `docker compose config --quiet`, `.\scripts\test-setup.ps1`.
5. Áp dụng theo bảng mục 7.4 và cập nhật docs khi key ảnh hưởng dev khác.

### 7.6 Tài liệu liên quan

- [QUICK_START.md](QUICK_START.md) — happy path, progress lines, known errors, flags và ports.
- [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md) — cấu trúc `setup.ps1`, runner app và cách backup script local.
- [schema-integration.md](docs/02-architecture/database/schema-integration.md) — cấu trúc DB, seed demo, naming container/DB.
- [workflow-upgrades-2026-10-08.md](docs/02-architecture/database/workflow-upgrades-2026-10-08.md) — lịch sử workflow migration.
- [scripts/database/seeds/README.md](scripts/database/seeds/README.md) — phân biệt seed demo với seed canonical.
- [SmartParking_Env_Config_SKILL_v1.0.md](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md) — quy tắc cập nhật `.env` và thêm key mới theo consumer.

### OperationService (đổi tên từ ReservationService)

Mã nguồn, project và namespace dùng tên OperationService. Chạy bằng -Service Operation; -Service Reservation vẫn là alias tương thích. Giữ UserSecretsId SmartParking.ReservationService.API.LocalDevelopment, các khóa ConnectionStrings:Reservation / Services:Reservation và biến SMARTPARK_*RESERVATION*. Compose vẫn dùng service/image reservation-service để tương thích môi trường hiện có; build arg là OperationService. Port API 5055, database smartpark_operations và các HTTP route giữ nguyên.

Sau khi pull, chạy dotnet restore SmartParking.slnx và dotnet build SmartParking.slnx. Với Docker, chạy scripts/run-app-service.ps1 -Service Operation để build lại image; không dùng -NoBuild cho lần đầu sau đổi tên.
