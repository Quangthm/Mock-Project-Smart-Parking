# Quick start — SmartParking

Hướng dẫn onboarding cho dev mới. Làm theo thứ tự: **Prerequisites → First-time Setup → Run Application → Verify Setup**. Các phần **Optional Features**, **Troubleshooting**, **Advanced Options** có thể bỏ qua ở lần chạy đầu.

> Chạy PowerShell ngay tại root của checkout; mọi lệnh trong file không phụ thuộc đường dẫn máy cá nhân. `setup.ps1` được lưu trong Git — không cần xin script từ kênh nội bộ.

## TL;DR happy path

```powershell
git branch --show-current          # xác nhận đang ở branch làm việc
.\setup.ps1                        # dựng DB + cấu hình credential + SonarQube
.\scripts\run-app-service.ps1 -Service All   # chạy 4 container app (BE + FE)
# Mở http://localhost:5173
```

Chi tiết từng bước và cách xử lý lỗi nằm ở các mục bên dưới.

## 1. Prerequisites

Checklist trước khi setup:

- [ ] **Windows PowerShell 5.1+** — script hỗ trợ 5.1, không cần PowerShell 7.
- [ ] **.NET SDK 10** — `dotnet --version` phải trả major 10.
- [ ] **Docker Desktop đang chạy** và Docker Compose hỗ trợ `up --wait` (Compose v2.17+).
- [ ] **Node.js + npm** nếu chạy frontend trực tiếp trên host (FE dùng Vite 8). Docker app stack dùng Node 22 trong image, không cần cài Node trên host.
- [ ] **Git** và checkout đúng branch: `git branch --show-current`.
- [ ] **Port đang rảnh** (xem bảng đầy đủ ở [mục 7](#7-advanced-options)):
  - API: `5035`, `5045`, `5055` · FE: `5173` · SonarQube: `9000`
  - DB: `5441`–`5446` · AI (tùy chọn): `5001`, `5002`

### Thuật ngữ nhanh cho người mới

| Thuật ngữ | Nghĩa ngắn gọn |
| --- | --- |
| **Service key** | Khóa chia sẻ giữa các service khi gọi nội bộ; phải dài **ít nhất 32 ký tự** (`SMARTPARK_SERVICE_KEY`). |
| **user-secrets** | Kho secret của .NET dùng khi chạy API trực tiếp từ Visual Studio/`dotnet run`. `setup.ps1` đồng bộ từ `.env` ở bước `[5/7]`. |
| **Runner** | Script chạy một API với biến môi trường nạp từ `.env` (ví dụ `scripts/run-schema-service.ps1`), không phụ thuộc user-secrets. |
| **Schema volume** | Named volume Docker giữ dữ liệu 6 PostgreSQL DB. Xóa volume = mất dữ liệu dev. |
| **Seed** | Tạo dữ liệu mẫu (site/operator/capacity) theo kiểu idempotent — chạy lại không nhân bản dữ liệu. |
| **`smartpark-schema_default`** | External Docker network do stack DB tạo; Docker app stack dùng lại network này. |

## 2. First-time Setup

```powershell
.\setup.ps1
```

`setup.ps1` chạy 7 bước, in mốc `[n/7]` trước mỗi bước:

| Bước | Việc được làm |
| --- | --- |
| `[1/7]` | Kiểm tra prerequisites (.NET SDK 10) và đọc `.env`. |
| `[2/7]` | Dùng lại credential đã có, chỉ sinh giá trị còn thiếu. |
| `[3/7]` | Validate service key (≥32 ký tự), policy password (8–15 ký tự, đủ hoa/thường/số/ký tự đặc biệt) và 6 port DB. |
| `[4/7]` | Dựng 6 DB `user`, `facility`, `operations`, `payment`, `notification`, `iot`, probe authentication, áp migration. |
| `[5/7]` | Đồng bộ user-secrets cho User/Parking/Reservation theo `.env`. |
| `[6/7]` | Chuẩn bị `FE/.env`, MapTiler, AI endpoints; khởi động SonarQube (mặc định). |
| `[7/7]` | `dotnet restore`. |

Mặc định `CredentialMode = Local`: credential trong `.env`/user-secrets được **dùng lại**, chỉ sinh khi thiếu. Setup kết thúc bằng dòng `Setup complete...` khi mọi bước pass.

### Xử lý prompt `.env` và cảnh báo ghi đè

Nếu `.env` đã tồn tại, setup hỏi `[O]verwrite / [K]eep / [A]bort` (**Enter = Keep**).

> **CẢNH BÁO — ghi đè cấu hình:** chọn `O` sẽ backup `.env` hiện tại ra `.env.bak` rồi **sinh lại `.env` từ `.env.example`**. Password/key đang dùng có thể bị thay; chỉ chọn `O` khi chấp nhận cấu hình lại. Prompt bị bỏ qua khi chạy `-NonInteractive` (tự động Keep).

> **CẢNH BÁO — mất dữ liệu:** **không chạy `-RecreateDb` ở lần setup đầu.** Flag này xóa schema volume và init lại 6 DB, làm mất toàn bộ dữ liệu dev. Chi tiết khi nào cần dùng ở [mục 7](#7-advanced-options).

Không commit `.env`, file Team credential, user-secrets hoặc thư mục workflow keys.

> Ghi chú: `setup.ps1` **không** tự chạy API/FE. Sau khi setup xong, chuyển sang [mục 3](#3-run-application).

## 3. Run Application

Chọn **một** trong hai cách; không chạy đồng thời cả hai vì cùng dùng port `5035/5045/5055`.

### Cách A — Docker app stack (khuyến nghị cho onboarding)

Không cần IDE hay cài .NET/Node trên host sau lần build đầu. Compose project `smartpark-app` dùng external network `smartpark-schema_default` và DB hostname `user-db`/`facility-db`/`operations-db`.

```powershell
.\scripts\run-app-service.ps1 -Service User       # 1 service, mở CMD log riêng
.\scripts\run-app-service.ps1 -Service Parking
.\scripts\run-app-service.ps1 -Service Reservation
.\scripts\run-app-service.ps1 -Service Frontend
.\scripts\run-app-service.ps1 -Service All        # cả 4 container
```

| Container | URL | Ghi chú |
| --- | --- | --- |
| `smartpark-app-user-service-1` | http://localhost:5035/health | Seed admin/owner/operator khi start |
| `smartpark-app-parking-service-1` | http://localhost:5045/health | Dùng `facility-db` |
| `smartpark-app-reservation-service-1` | http://localhost:5055/health | Dùng `operations-db` |
| `smartpark-app-frontend-1` | http://localhost:5173 | Vite dev + HMR, bind mount `FE/` |

> **Lưu ý chuyển tiếp v1.1 / Fix2FA:** ParkingService (dùng `facility-db` trên port 5442) đang được đổi tên thành FacilityService trong branch `Fix2FA` theo SRS v1.1 (quyết định V11-09). Tương tự, ReservationService (dùng `operations-db` trên port 5443) sẽ hợp nhất vào Parking Operations. Các port và contract API vẫn giữ nguyên tính tương thích.

- Lần đầu build 4 image (.NET 10 + Node 22) nên lâu; các lần sau dùng cache.
- `-NoBuild` bỏ qua bước build khi image đã có; `-NoLogWindow` không mở cửa sổ CMD (dùng cho CI/automation).
- Có thể double-click `scripts\run-*-service.cmd` hoặc `scripts\run-all-apps.cmd`.
- Dừng stack (giữ nguyên volume): `.\scripts\stop-app-service.ps1 -Service All`.
- Log một service: `docker compose --project-name smartpark-app --env-file .env -f compose.app.yml logs --tail 200 <service>`.

### Cách B — Runner trên host (Visual Studio / `dotnet run`)

Mở 3 terminal PowerShell tại repo root, mỗi terminal chạy một API:

```powershell
.\scripts\run-schema-service.ps1 -Service User
# Terminal khác:
.\scripts\run-schema-service.ps1 -Service Parking
# Terminal khác:
.\scripts\run-schema-service.ps1 -Service Reservation
```

Runner nạp `.env` vào process (connection string, service key, credential admin/owner/operator), nên không cần user-secrets. Khi chạy trực tiếp từ Visual Studio, dùng user-secrets đã được `[5/7]` đồng bộ theo `.env`; nếu tự sửa secrets sau đó, chạy lại `.\setup.ps1` để đồng bộ lại.

Frontend chạy trên host:

```powershell
cd FE
npm install
npm run dev
```

Vite chạy mặc định tại http://localhost:5173.

**MapTiler:** điền `VITE_MAPTILER_API_KEY` trong root `.env` hoặc file Team. Setup chỉ bổ sung nếu key trong `FE/.env` còn trống; key FE đã có được giữ nguyên.

## 4. Verify Setup

Checklist xác nhận môi trường chạy đúng:

- [ ] **6 DB container healthy**

  ```powershell
  docker compose --project-name smartpark-schema --env-file .env -f compose.schema-integration.yml ps
  ```

- [ ] **3 API trả health 200**

  ```powershell
  Invoke-WebRequest http://localhost:5035/health -UseBasicParsing
  Invoke-WebRequest http://localhost:5045/health -UseBasicParsing
  Invoke-WebRequest http://localhost:5055/health -UseBasicParsing
  ```

- [ ] **FE mở được** tại http://localhost:5173.
- [ ] **Đăng nhập được tài khoản demo** (email lấy từ `.env`, password ở các biến tương ứng):

  | Tài khoản | Email | Password |
  | --- | --- | --- |
  | Admin | `admin@smartpark.local` (hoặc `SMARTPARK_ADMIN_EMAIL`) | `SMARTPARK_ADMIN_PASSWORD` |
  | Owner | `owner@smartpark.local` (`SMARTPARK_OWNER_EMAIL`) | `SMARTPARK_OWNER_PASSWORD` |
  | Operator | `operator@smartpark.local` (`SMARTPARK_OPERATOR_EMAIL`) | `SMARTPARK_OPERATOR_PASSWORD` |
  | Driver demo | `driver@gmail.com` | `Password@123` (tạo trong DataSeeder) |

- [ ] **Seed site demo** sau khi UserService đã chạy lần đầu:

  ```powershell
  .\scripts\seed-demo-site.ps1
  ```

  Script tạo `DEMO-HQ` (Floor 1, Zone A, 6 slot `CAR` + 2 slot `MOTORCYCLE`), gán operator vào site, cấp 5 quyền vận hành (`DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`, `SLOT_OVERRIDE`) và cấu hình capacity. Chạy lại an toàn; script không in password. Nếu báo thiếu owner/operator, chạy `.\scripts\run-schema-service.ps1 -Service User` một lần rồi seed lại.

- [ ] **SonarQube** (mặc định setup tự dựng) mở được tại http://localhost:9000 — xem [mục 5.3](#53-sonarqube).

## 5. Optional Features

### 5.1 AI Camera (`-WithAiCamera`)

AI camera là tùy chọn riêng. **Không thêm flag thì không build/chạy gì thuộc AI.** Dùng khi cần REST API nhận diện (`ai-rest`) và luồng live (`ai-live`):

```powershell
.\setup.ps1 -WithAiCamera
```

- Kiểm tra 7 model file trong `src/Services/IoTService/python-ai/models` và network `smartpark-schema_default` **trước khi** gọi Docker cho AI.
- Dựng 2 container từ `compose.ai.yml` (project `smartpark-ai`): `ai-rest` tại http://localhost:5001, `ai-live` tại http://localhost:5002.
- Chỉ hợp lệ khi chạy full setup: cần schema DBs đang chạy (không dùng chung `-SkipDatabase`) và `SMARTPARK_DEV_PASSWORD` đã cấu hình trong `.env`.
- Lần build đầu ~5–10 phút (PyTorch CPU wheels + PaddleOCR warm-up) và tốn RAM; các lần sau dùng cache.

Setup **chỉ build khi cần**:

| Trạng thái | Hành vi |
| --- | --- |
| Image `smartpark-ai:local` chưa có | Build + `up -d --wait` |
| Image có, chưa có container | In playbook A–E, probe thư viện một lần, hỏi `[S] Start / [R] Rebuild / [C] Clean rebuild / [K] Skip AI / [A] Abort` (Enter = S). `-NonInteractive` mặc định Start, không tự rebuild |
| Image + container đã có | Bỏ qua build, chỉ `up -d --wait --no-build` |
| Container dùng image ID cũ | Cảnh báo, recreate bằng `--force-recreate --no-build`, **không** tự rebuild |
| Container unhealthy | Không tự rebuild, in log `--tail 200` và hướng dẫn |

- `-RebuildAi` (chỉ dùng cùng `-WithAiCamera`) buộc build lại **có cache** rồi start; dùng một mình sẽ throw.
- "Skip" chỉ bỏ build — setup vẫn gọi `up --no-build` để đảm bảo container chạy; chọn `K` mới bỏ toàn bộ AI.
- Kiểm tra nhanh:

  ```powershell
  docker compose -p smartpark-ai --env-file .env -f compose.ai.yml ps --all
  docker compose -p smartpark-ai --env-file .env -f compose.ai.yml logs --tail 200
  ```

### 5.2 Team credentials

Khi nhiều dev cần dùng chung credential, leader cung cấp **file credential riêng** (lưu ngoài repo):

```powershell
.\setup.ps1 -CredentialMode Team -TeamEnvPath '<file-env-riêng-của-team>'
```

- File Team bắt buộc có: `SMARTPARK_DEV_PASSWORD`, `SMARTPARK_SERVICE_KEY` (≥32 ký tự), `SMARTPARK_ADMIN_PASSWORD` (8–15 ký tự, đủ hoa/thường/số/ký tự đặc biệt).
- Có thể kèm: `SONAR_DB_PASSWORD`, `SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`, `VITE_MAPTILER_API_KEY`, `SMARTPARK_SERVICE_USER_URL`, `SMARTPARK_SERVICE_PARKING_URL`, `SMARTPARK_SERVICE_RESERVATION_URL`.
- File Team **không thay thế** toàn bộ `.env`: port, SMTP và cấu hình riêng trên máy vẫn giữ nguyên.
- Setup dừng nếu credential Team khác cấu hình local đã có (`Team/local conflict`); cấu hình hiện tại được giữ nguyên, phải reconcile thủ công rồi chạy lại.
- Không commit file Team hoặc secret thật.

### 5.3 SonarQube

SonarQube tự khởi động cùng `.\setup.ps1`; bỏ qua bằng `-SkipSonarQube`. `-SkipDatabase` không khởi động container nhưng vẫn dựng credential.

- `SONAR_DB_PASSWORD` được sinh tự động khi thiếu và giữ nguyên giá trị đã cấu hình.
- UI mặc định: http://localhost:9000 — lần đầu đăng nhập `admin/admin` và đổi password trên UI.
- Compose Sonar riêng giữ dữ liệu trong named volumes; setup không tự tiếp quản container Sonar cũ.
- Pin `SONAR_IMAGE` theo version team duyệt nếu cần môi trường cố định.
- Cấu hình dựa trên [ví dụ chính thức SonarSource](https://github.com/SonarSource/docker-sonarqube/blob/master/example-compose-files/sq-with-postgres/docker-compose.yml).

## 6. Troubleshooting

### 6.1 Progress lines `[n/7]` — dùng để định vị lỗi

`setup.ps1` in mốc `[n/7]` trước mỗi giai đoạn; giai đoạn DB còn có dòng con `[migrationIndex/6]`. Màu: header `[n/7]` = cyan, dòng con `- ...` = xám nhạt, hoàn tất mỗi bước = xanh lá, prompt/warning = vàng.

**Dòng progress cuối cùng trên màn hình = bước đang chạy khi lỗi xảy ra.**

| Progress line | Giai đoạn | Lỗi thường gặp → Cách xử lý |
| --- | --- | --- |
| `[1/7]` Checking prerequisites | .NET SDK 10, đọc `.env` | Chưa cài .NET SDK 10 → cài đúng major 10. `.env` đã tồn tại → prompt `[O]verwrite / [K]eep / [A]bort` (Enter = Keep; Overwrite backup ra `.env.bak` rồi sinh lại từ `.env.example`). `-NonInteractive` bỏ qua prompt. |
| `[2/7]` Resolving credentials | Sinh/dùng lại password, service key, admin/owner/operator password | `Existing database volumes found` → volume cũ cần lại password gốc trong `.env` (setup không tự xóa data); hoặc chạy `-RecreateDb` nếu chấp nhận mất data dev. `Existing service keys differ` → user-secrets 3 service lệch nhau, sửa tay. |
| `[3/7]` Validating configuration | Check key ≥32 ký tự, policy password, 6 port | `Invalid port` → biến port không phải số 1–65535. Password admin/owner/operator không đạt 8–15 ký tự/đủ loại ký tự → sửa trong `.env`. |
| `[4/7]` Initializing databases | Compose config → (drop volume nếu `-RecreateDb`) → `up --wait` 6 DB → probe auth → migration | Treo lâu ở `Starting database containers` → Docker Desktop chưa chạy / port `5441–5446` bị chiếm (`docker compose -f compose.schema-integration.yml ps` + `logs <tên-db>`). `authentication failed` → password trong `.env` khác password đã init volume (fix bằng `-RecreateDb` hoặc khôi phục password cũ). Lỗi SQL khi init → `docker logs <tên-db-container>`, dòng log chỉ đúng file `.sql` bị lỗi. Dòng con `[migrationIndex/6]` cho biết DB service nào (user→iot) đang fail. |
| `[5/7]` Configuring user-secrets | Ghi/đồng bộ connection string, URL, admin/owner/operator credential vào user-secrets theo `.env`; xóa khóa optional (JWT PEM, SMTP) không còn trong `.env` | `Cannot configure ... user-secrets` → thư mục UserSecrets bị khóa/đầy; chạy lại bằng `-SkipUserSecrets` rồi dùng runner. |
| `[6/7]` Preparing frontend environment | Copy `FE/.env`, MapTiler key, AI endpoints; khởi động SonarQube; nếu `-WithAiCamera`: kiểm tra model/network, dò image + container AI | `Configure MapTiler key directly in FE/.env` → key chứa ký tự đặc biệt, sửa tay trong `FE/.env`. `SonarQube startup failed` → volume Sonar cũ lệch `SONAR_DB_PASSWORD`, khôi phục password gốc vào `.env`. `AI model assets are missing` → khôi phục file model đã tracked. `Docker network smartpark-schema_default not found` → chạy setup không `-SkipDatabase` trước. |
| `[7/7]` Restoring dependencies | `dotnet restore` | Restore fail → kiểm tra mạng/proxy NuGet; hoặc bỏ qua với `-SkipRestore`. |

### 6.2 Lỗi đã biết — Triệu chứng → Nguyên nhân → Cách xử lý

**[4/7] — `ERROR: relation "users" already exists` (hoặc bảng base khác) ở dòng con `[migrationIndex/6]`**

- **Nguyên nhân:** docker init đã chạy đủ script SQL trên volume mới, nhưng runner áp lại cùng các file base.
- **Cách xử lý:** đã fix trong `apply-workflow-migrations.ps1` — probe sentinel trước khi áp: schema hiện tại → skip + verify, DB trống → apply, schema cũ → báo chạy `-RecreateDb`. Nếu vẫn gặp, kiểm tra volume đang dùng đúng chưa và xem log container DB.

**[5/7] — `password authentication failed for user "smartpark_user"` (bug 28P01) khi chạy BE trực tiếp từ Visual Studio/`dotnet run`**

- **Triệu chứng:** chạy qua runner `run-schema-service.ps1` không bị, nhưng VS/`dotnet run` báo 28P01.
- **Nguyên nhân:** user-secrets lưu connection string chứa password cũ, lệch với password volume PostgreSQL đã init; `DbConnectionStringBuilder` không tokenize được giá trị legacy nên `[2/7]` không đọc lại được password, và `[5/7]` cũ chỉ ghi key thiếu nên không bao giờ sửa giá trị lệch.
- **Cách xử lý:** đã fix — `[5/7]` ghi đè mọi giá trị user-secrets lệch theo `.env` mỗi lần chạy (và xóa khóa optional không còn trong `.env`), `[2/7]` parse password bằng regex fallback khi tokenizer fail, các conflict `... differs from .env` ở `[3/7]` bị bỏ. Chạy lại `.\setup.ps1` để đồng bộ.

**[6/7] (ai-rest/ai-live) — `Docker network smartpark-schema_default not found`**

- **Nguyên nhân:** network này chỉ tồn tại sau khi schema DBs được dựng.
- **Cách xử lý:** chạy setup **không** `-SkipDatabase` trước, rồi chạy lại với `-WithAiCamera`.

**[6/7] (ai-rest/ai-live) — build lần đầu chậm (~5–10 phút)**

- **Nguyên nhân:** PyTorch CPU wheels + PaddleOCR warm-up.
- **Cách xử lý:** bình thường; các lần sau dùng cache và setup bỏ qua build nếu image/container đã có.

**[6/7] (ai-rest/ai-live) — cảnh báo container dùng image cũ**

- **Nguyên nhân:** image `smartpark-ai:local` đã được build lại nhưng container còn trỏ image ID cũ.
- **Cách xử lý:** setup không tự rebuild mà recreate bằng `--force-recreate --no-build`. Chỉ dùng `.\setup.ps1 -WithAiCamera -RebuildAi` khi image hiện tại hỏng/thiếu thư viện.

**[6/7] (ai-rest/ai-live) — container báo unhealthy**

- **Nguyên nhân:** container/image lỗi hoặc thiếu package.
- **Cách xử lý:** xem log `docker compose -p smartpark-ai --env-file .env -f compose.ai.yml logs --tail 200`, rồi chỉ rebuild có xác nhận bằng `-RebuildAi` nếu image là nguyên nhân. Khi image có nhưng chưa có container: chọn `S` dùng image sẵn có, `R`/`C` rebuild có cache/sạch, `K` bỏ AI, `A` dừng setup.

**Ghi chú (ai-rest/ai-live):** kết nối DB trong container dùng biến môi trường rời (`IOT_DATABASE_HOST=iot-db`, port `5432`), không dùng DSN URL vì password có thể chứa ký tự `@`.

**Volume cũ (schema cũ hơn baseline hiện tại):** chạy `.\setup.ps1 -RecreateDb` để build lại 6 DB theo schema canonical; runner tự phân biệt DB trống / schema hiện tại / schema cũ và chỉ áp script khi cần. Chi tiết ở [LOCAL-SETUP.md](LOCAL-SETUP.md).

**Frontend (Docker) — `[vite] (client) Pre-transform error: Failed to resolve import "<package>" from "src/..."`**

- **Triệu chứng:** Container `smartpark-app-frontend-1` báo lỗi `Internal server error: Failed to resolve import "..."` hoặc `Pre-transform error: Failed to resolve import ... Does the file exist?` (ví dụ `motion/react`, `animejs`).
- **Nguyên nhân:** Mã nguồn vừa thêm dependency mới vào `FE/package.json`, nhưng Docker Compose đang mount named volume `fe-node-modules` vào `/app/node_modules`. Docker giữ nguyên cache volume cũ từ các lần chạy trước chứ không tự động đồng bộ package mới.
- **Cách xử lý:** Chọn một trong hai cách:
  - **Cách 1 (Nhanh nhất — không cần restart):** Chạy `npm install` trực tiếp vào container đang chạy:
    ```powershell
    docker exec -it smartpark-app-frontend-1 npm install
    ```
  - **Cách 2 (Làm mới volume sạch):** Dừng frontend, xóa volume cache cũ và build/chạy lại:
    ```powershell
    docker compose -f compose.app.yml stop frontend
    docker volume rm smartpark-app_fe-node-modules
    .\scripts\run-app-service.ps1 -Service Frontend
    ```
  *(Nếu chạy frontend trên host bằng `npm run dev`, chỉ cần chạy `cd FE; npm install`)*

> Khi gặp lỗi mới: **note lại dòng `[n/7]` (hoặc `[migrationIndex/6]`) + thông báo lỗi** vào mục này để dev sau tra cứu nhanh.

## 7. Advanced Options

### 7.1 Flags của `setup.ps1`

| Flag | Ý nghĩa |
| --- | --- |
| `-SkipDatabase` | Bỏ qua bước `[4/7]`: không init DB, không migrate. Vẫn dựng credential. |
| `-SkipRestore` | Bỏ qua bước `[7/7]`: không `dotnet restore`. |
| `-SkipUserSecrets` | Bỏ qua bước `[5/7]`: giữ nguyên user-secrets cũ. |
| `-RecreateDb` | **Phá hủy:** xóa schema volume rồi init lại 6 DB (mất dữ liệu dev). Chỉ dùng khi schema layout thay đổi. |
| `-CredentialMode Team -TeamEnvPath <file>` | Dùng credential từ file team thay vì sinh local. Xem [mục 5.2](#52-team-credentials). |
| `-SkipSonarQube` | Không khởi động SonarQube (mặc định chạy cùng setup). |
| `-NonInteractive` | Bỏ qua prompt `.env` overwrite; tự động Keep nếu `.env` đã tồn tại. |
| `-WithAiCamera` | Kiểm tra model/network và dựng 2 container AI (`ai-rest` :5001, `ai-live` :5002). Chỉ build khi image chưa có hoặc khi có `-RebuildAi`. Yêu cầu schema DBs đang chạy. |
| `-RebuildAi` | Đi cùng `-WithAiCamera`: buộc build lại image AI có cache rồi start; không dùng một mình. |
| `-WithSonarQube` | **Deprecated:** SonarQube đã chạy mặc định; flag vẫn được nhận để lệnh cũ không lỗi nhưng không còn tác dụng. |

> **CẢNH BÁO — trước khi chạy `-RecreateDb`:** flag này xóa schema volume và làm mất toàn bộ dữ liệu dev trong 6 DB. Chỉ chạy khi chấp nhận mất dữ liệu hoặc khi schema layout đã đổi và bắt buộc init lại.

### 7.2 Credentials, user-secrets và DB volume

- **Local reuse:** setup dùng lại credential đã có trong `.env`/user-secrets; chỉ sinh khi thiếu. Ba API dùng cùng service key trên máy này.
- **`Team/local conflict`:** setup dừng nếu credential Team khác cấu hình local đã có; không tự ghi đè để tránh phá môi trường đang chạy.
- **`[5/7]` ghi đè user-secrets:** mỗi lần chạy setup, user-secrets được đồng bộ theo `.env` — giá trị lệch bị ghi đè, khóa optional (JWT PEM, SMTP) không còn trong `.env` bị xóa. Muốn giữ user-secrets cũ thì dùng `-SkipUserSecrets` + runner, **không** chạy trực tiếp từ Visual Studio.
- **Password volume DB:** đổi password trong `.env` **không** đổi password đã init trong PostgreSQL volume. Nếu volume cũ có password khác, phải khôi phục password gốc trong `.env`; chỉ `-RecreateDb` mới tạo lại DB với password mới (kèm mất dữ liệu).
- **Tài khoản seed:** admin/owner/operator được UserService seed khi service khởi động lần đầu; đổi password trong `.env` không đổi password đã seed trong DB. Driver demo `driver@gmail.com / Password@123` được DataSeeder tạo trong code.

### 7.3 Ports

| Thành phần | Port | Ghi chú |
| --- | --- | --- |
| User/Parking/Reservation API | `5035` / `5045` / `5055` | Docker app stack bind `127.0.0.1` |
| User/Parking/Reservation DB | `5441` / `5442` / `5443` | Bind loopback (`127.0.0.1`) |
| Payment/Notification/IoT DB | `5444` / `5445` / `5446` | Bind loopback (`127.0.0.1`) |
| `ai-rest` / `ai-live` | `5001` / `5002` | Chỉ dựng bằng `.\setup.ps1 -WithAiCamera` |
| Frontend (Vite) | `5173` | Host hoặc Docker |
| SonarQube UI | `9000` | Mặc định của setup |

DB đều bind loopback. Giao tiếp API xuyên máy cần URL host truy cập được, listener/firewall phù hợp và cùng service key; setup không tự mở mạng. Cấu hình URL qua `SMARTPARK_SERVICE_USER_URL` / `SMARTPARK_SERVICE_PARKING_URL` / `SMARTPARK_SERVICE_RESERVATION_URL`.

### 7.4 DB volume và migration

- Stack DB: 6 service `user-db` / `facility-db` / `operations-db` / `payment-db` / `notification-db` / `iot-db`; DB vật lý `smartpark_user` / `smartpark_facility` / `smartpark_operations` / `smartpark_payment` / `smartpark_notification` / `smartpark_iot`.
- Container name cố định cho DB chính: `facility-db-1`, `operations-db-1` (dùng trong script/DB tool).
- Runner migration probe trạng thái: DB trống → áp canonical; schema hiện tại → skip + verify; schema cũ → báo chạy `-RecreateDb`.
- Chuyển từ script setup cũ: xem [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md).

### 7.5 Cập nhật `.env` — chạy lại script gì

Đổi giá trị trong `.env` không cần chạy full setup; chạy đúng đích theo bảng:

| Đổi giá trị | Chạy lại |
| --- | --- |
| Giá trị container dùng (`SMARTPARK_SERVICE_KEY`, password admin/owner/operator) | `.\scripts\run-app-service.ps1 -Service All -NoBuild` — recreate container với env mới, không cần build image |
| Giá trị runner host dùng (mọi key trong `.env`) | Restart `.\scripts\run-schema-service.ps1 -Service X` (runner đọc `.env` mỗi lần start) |
| Chạy Visual Studio/`dotnet run` (user-secrets) | `.\setup.ps1 -SkipDatabase -SkipRestore` — bước `[5/7]` ghi đè user-secrets lệch |
| Port DB `SMARTPARK_DB_*_PORT` | `.\setup.ps1 -SkipRestore` rồi restart runner |
| MapTiler / AI key cho FE | Sửa trực tiếp `FE/.env` + restart Vite — setup không ghi đè file đã có giá trị |
| `VITE_*` của FE container / port trong compose | Sửa `compose.app.yml` rồi chạy lại runner |

> **Ngoại lệ — password gắn với DB volume:** đổi `SMARTPARK_DEV_PASSWORD` (hoặc password tài khoản seed) khi volume đã init không áp dụng vào PostgreSQL — phải khôi phục giá trị gốc, `-RecreateDb` (mất dữ liệu), hoặc đổi trong DB. Container sẽ unhealthy; setup báo `authentication failed`.

Luôn start container qua `run-app-service.ps1` (hoặc `.cmd`) — nút Start trong Docker Desktop / `docker start` chạy lại image và env cũ, không build/recreate.

### 7.6 Thêm key mới vào `.env.example` và `.env`

Thêm key vào `.env.example` chưa đủ — phải wire theo consumer và tự thêm giá trị vào `.env` local (setup không copy key lạ vào `.env` đã tồn tại):

- BE container → thêm `environment:` trong `compose.app.yml` dạng `NewKey: ${NEW_KEY:?...}`.
- BE host runner → key đặt tên `Section__Key` dùng được ngay; tên `SMARTPARK_*` cần thêm dòng mapping trong `run-schema-service.ps1`.
- VS/user-secrets → thêm vào `$desired` bước `[5/7]` của `setup.ps1` (optional key: thêm cả `$removable`).
- Sinh/validate tự động → bổ sung bước `[2/7]`/`[3/7]`.
- FE host → `FE/.env.example` (+ bước `[6/7]` nếu muốn auto-fill); FE container → `environment:` của service `frontend`.

Checklist đầy đủ và lệnh kiểm chứng: [SmartParking_Env_Config_SKILL_v1.0.md](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md).

### 7.7 Tài liệu liên quan

- [LOCAL-SETUP.md](LOCAL-SETUP.md) — setup local, Docker app stack, volume, AI camera và credential chi tiết.
- [UPDATE_SCRIPT_GUIDE.md](UPDATE_SCRIPT_GUIDE.md) — cấu trúc `setup.ps1`, runner app và cách sửa script.
- [schema-integration.md](docs/02-architecture/database/schema-integration.md) — cấu trúc DB, seed demo và naming.
- [workflow-upgrades-2026-10-08.md](docs/02-architecture/database/workflow-upgrades-2026-10-08.md) — lịch sử workflow migration.
- [scripts/database/seeds/README.md](scripts/database/seeds/README.md) — phân biệt seed demo với seed canonical.
- [SmartParking_Env_Config_SKILL_v1.0.md](docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md) — quy tắc cập nhật `.env` và thêm key mới theo consumer.
