# QUICK START - SmartPark local onboarding

Tài liệu này dành cho dev mới clone repo lần đầu trên Windows và muốn dựng đầy
đủ database + cấu hình để chạy backend/frontend.

> `setup.ps1` nằm trong `.gitignore`, **không** có trong git. Hãy nhận file này
> từ kênh nội bộ của team: `<KÊNH NỘI BỘ CỦA TEAM - ví dụ: Teams/Slack #smartpark-setup>`
> rồi đặt tại thư mục gốc repo (ngang hàng với `.env.example`).

---

## 1. Yêu cầu môi trường

| Thành phần | Yêu cầu | Kiểm tra nhanh |
| --- | --- | --- |
| OS | Windows | - |
| Docker | Docker Desktop **đang chạy** (có `docker` CLI) | `docker info` |
| Docker Compose | Compose plugin (`docker compose`) hoặc fallback `docker-compose` | `docker compose version` |
| .NET | .NET SDK 10 (script cảnh báo nếu major khác 10) | `dotnet --version` |
| PowerShell | Windows PowerShell >= 5.1 | `$PSVersionTable.PSVersion` |

Ghi chú:

- Kiểm tra Docker Desktop đã bật: `docker info` phải in ra `Server:` với thông
  tin daemon. Nếu báo lỗi `failed to connect to the docker API ...`, hãy mở
  Docker Desktop và chờ tới khi nó báo "running".
- Bước `[0/7] Prerequisites` của `setup.ps1` sẽ tự kiểm tra 4 mục trên; thiếu
  thành phần bắt buộc thì script dừng ngay.

---

## 2. Lấy `setup.ps1`

1. Nhận `setup.ps1` từ kênh nội bộ của team.
2. Đặt file tại **thư mục gốc repo** (cùng chỗ với `.env.example`).
3. Kiểm tra đã đúng vị trí:

```powershell
Test-Path .\setup.ps1          # => True
Test-Path .\.env.example       # => True
```

Các file sau **có trong git** và không cần xin từ kênh nội bộ:
`.env.example`, `FE/.env.example`, `scripts/load-env.ps1`.

---

## 3. Chạy setup lần đầu

Tại thư mục gốc repo:

```powershell
.\setup.ps1
```

Nếu bị chặn bởi Execution Policy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
```

Script chạy tuần tự theo các nhãn log `[n/7]` (tổng cộng 8 nhãn, mẫu số là `7`):

| Bước | Việc script làm |
| --- | --- |
| `[0/7] Prerequisites` | Kiểm tra `docker`, `docker compose` (fallback `docker-compose`), `dotnet` (khuyến nghị SDK 10), PowerShell >= 5.1. Thiếu → `FAIL`, dừng. |
| `[1/7] Env bootstrap` | Chưa có `.env` → copy từ `.env.example` rồi **tự điền 3 giá trị dùng chung** (`SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN_PASSWORD`, `SONAR_DB_PASSWORD`). Đã có `.env` → hỏi `O`verwrite / `K`eep / `A`bort (Enter = Keep). Tạo `FE\.env` từ `FE\.env.example` kèm `VITE_MAPTILER_API_KEY` dùng chung nếu chưa có. |
| `[2/7] Validate` | Nạp `.env` và kiểm tra biến bắt buộc. Lỗi → liệt kê từng key và dừng. |
| `[3/7] Docker DB` | `docker compose -f compose.schema-integration.yml up -d --wait` → 6 PostgreSQL container. |
| `[4/7] Migrations` | Gọi `scripts\apply-workflow-migrations.ps1` cho `User`, `Parking`, `Reservation`. |
| `[5/7] SonarQube` | Tạo network `smartpark-net` + 2 container `sonarqube-db`, `sonarqube` (idempotent). |
| `[6/7] User-secrets` | Ghi cấu hình vào User Secrets của 3 API project, in lại **key = value** (giá trị in rõ theo quyết định của team, có cảnh báo). |
| `[7/7] Summary` | In bảng trạng thái từng bước + next steps. |

Mỗi bước in `[n/7] <mô tả> ... OK|SKIP|FAIL`. Gặp `FAIL`, script in bảng tổng
kết và dừng (fail-fast). Script chạy lại nhiều lần được (idempotent).

**Lần đầu bắt buộc sửa `.env`:** file `.env` mới được copy từ `.env.example`
chứa placeholder dạng `replace-with-...`. `setup.ps1` tự điền 3 giá trị dùng
chung của team (`SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN_PASSWORD`,
`SONAR_DB_PASSWORD`); các placeholder còn lại phải thay trước khi setup thành
công. Ví dụ:

```powershell
notepad .env
```

Nếu chưa thay placeholder, bước `[2/7] Validate` sẽ `FAIL` với các dòng kiểu:
`SMARTPARK_DEV_PASSWORD still uses the .env.example placeholder.`

---

## 4. Bảng biến `.env` (root)

Các giá trị dưới đây chỉ là ví dụ, **không phải secret thật**. File thật là
`.env` (đã được gitignore).

| Key | Bắt buộc | Mô tả | Ví dụ |
| --- | --- | --- | --- |
| `SMARTPARK_DEV_PASSWORD` | **Bắt buộc** | Password dùng chung cho 6 PostgreSQL container local. Không được rỗng và không được là placeholder. | `My-Local-Db-Pass!` |
| `SMARTPARK_SERVICE_KEY` | **Bắt buộc** | Key nội bộ giữa các service, độ dài **>= 32 ký tự**. **`setup.ps1` tự điền giá trị dùng chung của team.** | key ngẫu nhiên >= 32 ký tự |
| `SMARTPARK_ADMIN_EMAIL` | Tùy chọn | Email admin seed cho UserService. Trống → dùng `admin@smartpark.local`. | `admin@smartpark.local` |
| `SMARTPARK_ADMIN_PASSWORD` | Tùy chọn | Password admin seed. Nếu trống thì `DevelopmentAdmin:Password` **không** được ghi vào user-secrets. **`setup.ps1` tự điền giá trị dùng chung của team.** | `Admin@12345` |
| `SMARTPARK_DB_USER_PORT` | Tùy chọn | Host port cho `user-db`. Trống → `5441`. | `5441` |
| `SMARTPARK_DB_PARKING_PORT` | Tùy chọn | Host port cho `parking-db`. Trống → `5442`. | `5442` |
| `SMARTPARK_DB_RESERVATION_PORT` | Tùy chọn | Host port cho `reservation-db`. Trống → `5443`. | `5443` |
| `SMARTPARK_DB_PAYMENT_PORT` | Tùy chọn | Host port cho `payment-db`. Trống → `5444`. | `5444` |
| `SMARTPARK_DB_NOTIFICATION_PORT` | Tùy chọn | Host port cho `notification-db`. Trống → `5445`. | `5445` |
| `SMARTPARK_DB_IOT_PORT` | Tùy chọn | Host port cho `iot-db`. Trống → `5446`. | `5446` |
| `SMARTPARK_SMTP_HOST` | Tùy chọn | SMTP host cho OTP email. Thiếu nhóm SMTP → chỉ cảnh báo. | `smtp.gmail.com` |
| `SMARTPARK_SMTP_PORT` | Tùy chọn | SMTP port. | `587` |
| `SMARTPARK_SMTP_USERNAME` | Tùy chọn | SMTP username. | `dev@example.com` |
| `SMARTPARK_SMTP_PASSWORD` | Tùy chọn | SMTP password / app password. | `<app-password>` |
| `SMARTPARK_SMTP_FROM` | Tùy chọn | Địa chỉ From của email OTP. | `dev@example.com` |
| `SMARTPARK_JWT_PRIVATE_KEY_PEM` | Tùy chọn | JWT private key PEM cho UserService. Trống → dùng development key (có cảnh báo). | (trống) |
| `SMARTPARK_WORKFLOW_KEY_DIRECTORY` | Tùy chọn | Thư mục lưu workflow signing key. Trống → `.cache/workflow-keys`. | `.cache/workflow-keys` |
| `SONAR_PORT` | Tùy chọn | Host port cho SonarQube UI. Trống → `9000`. | `9000` |
| `SONAR_DB_NAME` | Tùy chọn | Database name của SonarQube. Trống → `sonar`. | `sonar` |
| `SONAR_DB_USER` | Tùy chọn | User của SonarQube DB. Trống → `sonar`. | `sonar` |
| `SONAR_DB_PASSWORD` | Bắt buộc khi dựng SonarQube | Password cho `sonarqube-db`. Rỗng → bước `[5/7]` `FAIL`. **`setup.ps1` tự điền giá trị dùng chung của team.** | `Sonar-Local-Pass!` |

Chi tiết validate ở bước `[2/7]`:

- `FAIL`: `SMARTPARK_DEV_PASSWORD` rỗng/placeholder; `SMARTPARK_SERVICE_KEY`
  thiếu/placeholder/< 32 ký tự.
- Cảnh báo (không chặn): nhóm SMTP thiếu, `SMARTPARK_JWT_PRIVATE_KEY_PEM`
  trống, và danh sách key còn dùng placeholder `replace-with-...`.

---

## 5. Bảng port

| Thành phần | Port | Ghi chú |
| --- | --- | --- |
| PostgreSQL `user-db` | `5441` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_USER_PORT` |
| PostgreSQL `parking-db` | `5442` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_PARKING_PORT` |
| PostgreSQL `reservation-db` | `5443` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_RESERVATION_PORT` |
| PostgreSQL `payment-db` | `5444` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_PAYMENT_PORT` |
| PostgreSQL `notification-db` | `5445` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_NOTIFICATION_PORT` |
| PostgreSQL `iot-db` | `5446` | bind `127.0.0.1`, đổi được bằng `SMARTPARK_DB_IOT_PORT` |
| UserService API | `5035` | |
| ParkingService API | `5045` | |
| ReservationService API | `5055` | |
| Frontend (Vite dev) | `5173` | Vite default; `FE/vite.config.ts` không set port riêng |
| SonarQube UI | `9000` | đổi được bằng `SONAR_PORT` |

---

## 6. Chạy API và Frontend

### Backend

Sau khi `setup.ps1` hoàn tất (đã có database + user-secrets), chạy từng API
trong terminal riêng tại thư mục gốc repo:

```powershell
.\scripts\run-schema-service.ps1 -Service User
.\scripts\run-schema-service.ps1 -Service Parking
.\scripts\run-schema-service.ps1 -Service Reservation
```

`run-schema-service.ps1` sẽ:

1. Đọc root `.env` qua `scripts/load-env.ps1` (nếu thiếu `.env` → báo
   `Run setup.ps1 at the repository root first.`).
2. Validate `SMARTPARK_DEV_PASSWORD` và `SMARTPARK_SERVICE_KEY` (>= 32).
3. Set `ASPNETCORE_ENVIRONMENT=Development`, `Services__Key`,
   `Services__User/Parking/Reservation`, `ConnectionStrings__<Service>` theo
   port trong `.env`.
4. Nếu là `User` và có `SMARTPARK_ADMIN_PASSWORD`, set thêm
   `DevelopmentAdmin__Email/Password`.
5. Chạy `dotnet run --project <API> --no-restore --no-launch-profile -- ...`.

### Frontend

`setup.ps1` (bước `[1/7]`) đã tạo `FE\.env` từ `FE\.env.example` và tự điền
`VITE_MAPTILER_API_KEY` dùng chung của team. Chỉ cần:

```powershell
cd FE
npm install
npm run dev
```

`FE/package.json` khai báo các script: `dev` (`vite`), `build` (`vite build`),
`preview` (`vite preview`), `format` (`oxfmt`).

> Lưu ý: repo hiện có `FE/package-lock.json` → dùng **npm** cho nhất quán. Nếu
> team chuyển sang pnpm, hãy xóa `package-lock.json`, thêm `pnpm-lock.yaml` và
> cập nhật `FE/AGENTS.md` cho khớp.

`FE/.env` chỉ chứa cấu hình frontend (khác với root `.env` dùng cho infra/BE):

| Key | Mô tả | Ví dụ |
| --- | --- | --- |
| `VITE_API_BASE_URL` | Base URL UserService | `http://localhost:5035` |
| `VITE_PARKING_API_BASE_URL` | Base URL ParkingService | `http://localhost:5045` |
| `VITE_MAPTILER_API_KEY` | API key MapTiler cho bản đồ. **`setup.ps1` tự điền key dùng chung của team khi tạo `FE\.env`.** | `<maptiler-key>` |

---

## 7. SonarQube

- UI: `http://localhost:9000` (hoặc `http://localhost:$SONAR_PORT`).
- Đăng nhập lần đầu: **`admin` / `admin`**.
- **PHẢI đổi mật khẩu** ở lần đăng nhập đầu tiên. SonarQube Community **không
  thể** set mật khẩu admin qua biến môi trường.
- Container liên quan: `sonarqube`, `sonarqube-db`; network `smartpark-net`.
- `setup.ps1` là idempotent: nếu container đã tồn tại thì tái sử dụng (start lại
  nếu đang dừng), không tạo trùng.

---

## 8. Troubleshooting

### Docker Desktop chưa chạy

Bước `[0/7]` có thể vẫn `OK` (vì `docker --version` chạy được cả khi daemon
tắt), nhưng bước `[3/7]` sẽ `FAIL` với:

```text
docker compose up failed. Is Docker Desktop running, and is .env configured (step 2)?
```

hoặc bước `[5/7]` báo:

```text
Could not list Docker containers. Is Docker Desktop running?
```

Khắc phục: mở Docker Desktop, chờ tới khi daemon `running`, rồi chạy lại
`.\setup.ps1`.

### Placeholder chưa thay

Bước `[2/7]` báo:

```text
SMARTPARK_DEV_PASSWORD still uses the .env.example placeholder.
```

Khắc phục: mở `.env`, thay các giá trị `replace-with-...` còn lại (3 key dùng
chung `SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN_PASSWORD`, `SONAR_DB_PASSWORD`
đã được `setup.ps1` tự điền) bằng giá trị thật của bạn, chạy lại `.\setup.ps1`.

### Service key quá ngắn

```text
SMARTPARK_SERVICE_KEY must be at least 32 characters (current length: N).
```

Khắc phục: dùng key ngẫu nhiên >= 32 ký tự.

### Thiếu `.env`

`scripts/load-env.ps1` báo khi file không tồn tại:

```text
Environment file not found: <path>\.env. Run setup.ps1 at the repository root first.
```

`run-schema-service.ps1` báo:

```text
Missing <path>\.env. Run setup.ps1 at the repository root first.
```

Khắc phục: chạy `.\setup.ps1` ở repo root.

### Volume cũ không nhận init SQL

Các script SQL trong `docker-entrypoint-initdb.d` **chỉ chạy khi volume được tạo
lần đầu**. Nếu volume đã tồn tại từ trước, đổi password/schema trong `.env`
không làm DB hiện có thay đổi.

Reset an toàn (⚠️ **XÓA TOÀN BỘ DỮ LIỆU local**, chỉ dùng cho môi trường dev):

```powershell
docker compose -f compose.schema-integration.yml down -v
.\setup.ps1
```

### Container SonarQube

Đổi cấu hình (port, DB) nhưng container đã tồn tại thì setup sẽ tái sử dụng cấu
hình cũ. Muốn tạo lại (⚠️ mất dữ liệu phân tích local):

```powershell
docker rm -f sonarqube sonarqube-db
docker volume rm sonarqube_db_data sonarqube_data_extensions sonarqube_data_logs
.\setup.ps1
```

Tên liên quan: container `sonarqube`, `sonarqube-db`; network `smartpark-net`.

---

## 9. Mapping `.env` → User Secrets

`setup.ps1` bước `[6/7]` ghi user-secrets bằng cách pipe JSON vào
`dotnet user-secrets set --project <project>`, sau đó gọi `dotnet user-secrets list`
chỉ để **in tên key** (giá trị bị ẩn). 3 project:

- `src/Services/UserService/SmartParking.UserService.API`
- `src/Services/ParkingService/SmartParking.ParkingService.API`
- `src/Services/ReservationService/SmartParking.ReservationService.API`

| `.env` key | Secret key | Project |
| --- | --- | --- |
| `SMARTPARK_SERVICE_KEY` | `Services:Key` | User, Parking, Reservation |
| (cố định) | `Services:User` = `http://localhost:5035/` | User, Parking, Reservation |
| (cố định) | `Services:Parking` = `http://localhost:5045/` | User, Parking, Reservation |
| (cố định) | `Services:Reservation` = `http://localhost:5055/` | User, Parking, Reservation |
| `SMARTPARK_DEV_PASSWORD` + `SMARTPARK_DB_<SVC>_PORT` | `ConnectionStrings:<Service>` (password được bọc trong dấu `"`) | Service tương ứng |
| `SMARTPARK_ADMIN_EMAIL` (mặc định `admin@smartpark.local`) | `DevelopmentAdmin:Email` | User |
| `SMARTPARK_ADMIN_PASSWORD` | `DevelopmentAdmin:Password` | User (chỉ set khi không rỗng) |
| `SMARTPARK_SMTP_HOST` | `Otp:Smtp:Host` | User |
| `SMARTPARK_SMTP_PORT` | `Otp:Smtp:Port` | User |
| `SMARTPARK_SMTP_USERNAME` | `Otp:Smtp:Username` | User |
| `SMARTPARK_SMTP_PASSWORD` | `Otp:Smtp:Password` | User |
| `SMARTPARK_SMTP_FROM` | `Otp:Smtp:From` | User |
| `SMARTPARK_JWT_PRIVATE_KEY_PEM` | `Jwt:PrivateKeyPem` | User |
| `SMARTPARK_WORKFLOW_KEY_DIRECTORY` (mặc định `.cache/workflow-keys`) | `Workflow:KeyDirectory` | User |

`ConnectionStrings:<Service>` có dạng:

```text
Host=127.0.0.1;Port=<db port>;Database=smartpark_<service>;Username=smartpark_<service>;Password="<dev password>"
```

Ví dụ `ConnectionStrings:User`:

```text
Host=127.0.0.1;Port=5441;Database=smartpark_user;Username=smartpark_user;Password="<dev password>"
```

---

## Assertions / mâu thuẫn cần biết

- **Số bước**: script log `[0/7]` → `[7/7]` (8 nhãn), mẫu số luôn là `7`.
- **`SMARTPARK_SCHEMA_ROOT`**: không còn trong `.env.example` và không còn file
  setup/compose nào dùng; chỉ còn vài script/tài liệu legacy nhắc tới
  (`scripts/qa-local.example.json`, `scripts/recover-structure-hold.ps1`, docs
  lịch sử). `setup.ps1` luôn resolve repo root để tìm `compose.schema-integration.yml`.
- **Volume SonarQube**: spec gốc ghi `sonarqube_data/extensions/logs` (chứa dấu
  `/`, không hợp lệ làm tên Docker volume); thực tế dùng 2 named volume
  `sonarqube_data_extensions` và `sonarqube_data_logs`, cộng `sonarqube_db_data`.
- **FE dev server**: `vite.config.ts` không set port → mặc định `5173`.
  `FE/AGENTS.md` nhắc `8443` là preview của Figma Make, không phải dev server local.
- `LOCAL-SETUP.md` đã cập nhật sang luồng `.env`; `docs/04-testing/database-integration-guide.md`
  giữ nội dung lịch sử của flow `arch/database-schema` nhưng đã có ghi chú trỏ về
  `setup.ps1`/`QUICK_START.md`.
