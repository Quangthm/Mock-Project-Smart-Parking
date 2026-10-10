# SmartParking

## Chạy local trên Windows

> Onboarding nhanh cho dev mới: xem [`QUICK_START.md`](QUICK_START.md).
> Muốn sửa/mở rộng bộ script: xem [`UPDATE_SCRIPT_GUIDE.md`](UPDATE_SCRIPT_GUIDE.md).

Cần Docker Desktop đang chạy, .NET SDK 10, Windows PowerShell >= 5.1 và Node.js
(npm) cho frontend. Chạy tại thư mục gốc repo:

```powershell
.\setup.ps1
```

`setup.ps1` là script setup gốc duy nhất, chạy tuần tự theo các nhãn log
`[0/7]`..`[7/7]`: kiểm tra prerequisites → tạo `.env` từ `.env.example` →
validate → dựng 6 PostgreSQL container → áp dụng migration → dựng SonarQube →
ghi user-secrets → tổng kết. Script idempotent (chạy lại được).

Cấu hình nằm trong `.env` ở thư mục gốc repo (đã được gitignore). `setup.ps1`
tự điền 3 giá trị dùng chung của team (`SMARTPARK_SERVICE_KEY`,
`SMARTPARK_ADMIN_PASSWORD`, `SONAR_DB_PASSWORD`); các placeholder
`replace-with-...` còn lại phải thay trước khi setup thành công. Bảng biến và
ý nghĩa: xem [`QUICK_START.md`](QUICK_START.md) mục 4.

`setup.ps1` **bị gitignore**, không có trong git: nhận file từ kênh nội bộ của
team và đặt tại thư mục gốc repo (ngang hàng `.env.example`, `scripts/load-env.ps1`
thì có trong git).

### Backend

Sau khi setup xong, mở ba terminal tại repo và chạy mỗi service một lệnh:

```powershell
.\scripts\run-schema-service.ps1 -Service User
.\scripts\run-schema-service.ps1 -Service Parking
.\scripts\run-schema-service.ps1 -Service Reservation
```

`run-schema-service.ps1` đọc root `.env` qua `scripts/load-env.ps1`, set
`Services__*` và `ConnectionStrings__<Service>` (port DB theo `.env`), rồi chạy
`dotnet run`. (Không còn dùng `.cache/qa-local.json`.)

| Service | HTTP API | PostgreSQL trên máy | Database |
| --- | --- | --- | --- |
| User | http://localhost:5035 | 5441 | smartpark_user |
| Parking | http://localhost:5045 | 5442 | smartpark_parking |
| Reservation | http://localhost:5055 | 5443 | smartpark_reservation |
| Payment | — | 5444 | smartpark_payment |
| Notification | — | 5445 | smartpark_notification |
| IoT | — | 5446 | smartpark_iot |

Compose hiện tại là `compose.schema-integration.yml`; host port DB đọc từ `.env`
(`SMARTPARK_DB_*_PORT`). Container `smartparking_db` trên cổng 5433 từ
`docker-compose.yml` thuộc bản cũ và không được setup dùng. Với volume đã có,
password local phải khớp password lúc khởi tạo volume — đổi biến môi trường
không đổi password trong PostgreSQL.

### Frontend và tài khoản local

`setup.ps1` (bước `[1/7]`) đã tạo `FE\.env` từ `FE\.env.example` và tự điền
`VITE_MAPTILER_API_KEY` dùng chung của team. Trong thư mục `FE`:

```powershell
npm install
npm run dev
```

Dev server Vite mặc định cổng `5173`. Xem `FE/.env.example` cho cấu hình API.

Admin phát triển dùng `admin@smartpark.local`; password lấy từ
`SMARTPARK_ADMIN_PASSWORD` trong `.env`. Setup không đổi password của Admin đã có.
Tài khoản trong database cũ không tự chuyển sang database mới.

Để gửi email OTP, điền nhóm `SMARTPARK_SMTP_*` trong `.env` rồi chạy lại
`.\setup.ps1`. Không commit cấu hình bí mật.

### SonarQube

UI tại `http://localhost:9000` (đổi được bằng `SONAR_PORT`); đăng nhập lần đầu
`admin/admin` và **phải đổi mật khẩu** (community edition không set được qua env).

### Lỗi thường gặp

- Docker Desktop chưa chạy → bước `[3/7]`/`[5/7]` `FAIL`; mở Docker Desktop rồi chạy lại.
- Placeholder chưa thay → bước `[2/7] Validate` `FAIL`; thay các placeholder
  `replace-with-...` còn lại (3 key dùng chung đã được `setup.ps1` tự điền).
- `Services:Key` thiếu/< 32 ký tự → kiểm tra `SMARTPARK_SERVICE_KEY` (thường đã
  được `setup.ps1` tự điền giá trị dùng chung).
- Volume cũ không nhận init SQL (init script chỉ chạy khi tạo volume lần đầu) →
  reset: `docker compose -f compose.schema-integration.yml down -v` (⚠️ mất toàn
  bộ dữ liệu local) rồi chạy lại `.\setup.ps1`.
- Port 5035/5045/5055 bị chiếm: dừng instance API cũ trước khi chạy instance mới.

Chi tiết: [QUICK_START.md](QUICK_START.md),
[schema integration](docs/02-architecture/database/schema-integration.md),
[workflow migrations](docs/02-architecture/database/workflow-upgrades-2026-10-08.md).
