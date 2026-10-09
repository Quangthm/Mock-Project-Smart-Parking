# SmartParking

## Chạy local trên Windows

Cần Docker Desktop đang chạy, .NET 10 SDK, PowerShell và Node.js để chạy frontend.
Chạy tại thư mục gốc repo:

```powershell
.\scripts\setup-local.ps1
```

Setup bật ba PostgreSQL container, áp dụng migration, restore dependency và nạp
cấu hình vào User Secrets của ba API để chạy bằng Visual Studio.
Nếu chưa có `.cache/qa-local.json`, script tạo file với password và service key
ngẫu nhiên. Nếu đã có, script dùng lại cấu hình. File này và key ring trong
`.cache/` được Git bỏ qua. Setup chạy lại được sau khi khởi động máy hoặc pull code.
`SMARTPARK_SCHEMA_ROOT` có thể trỏ tới checkout `arch/database-schema`; trên máy mới,
script mặc định dùng schema có sẵn trong repo này.

### Backend

Trong Visual Studio, chọn các API làm startup projects với profile `http` hoặc
`https`, môi trường `Development`, rồi chạy. Restart API nếu nó đang chạy khi setup.
Hoặc mở ba terminal tại repo, mỗi terminal chạy một lệnh:

```powershell
.\scripts\run-schema-service.ps1 -Service User
.\scripts\run-schema-service.ps1 -Service Parking
.\scripts\run-schema-service.ps1 -Service Reservation
```

| Service | HTTP API | PostgreSQL trên máy | Database |
| --- | --- | --- | --- |
| User | http://localhost:5035 | 5441 | smartpark_user |
| Parking | http://localhost:5045 | 5442 | smartpark_parking |
| Reservation | http://localhost:5055 | 5443 | smartpark_reservation |

Compose hiện tại là `compose.schema-integration.yml`. Container `smartparking_db`
trên cổng 5433 từ `docker-compose.yml` thuộc bản cũ; setup giữ dữ liệu database cũ
và dùng các database riêng. Với volume đã có, password local phải khớp password
khi khởi tạo volume; thay biến môi trường không đổi password trong PostgreSQL.

### Frontend và tài khoản local

Trong terminal tại thư mục `FE`, chạy `npm install` rồi `npm run dev`.
Xem `FE/.env.schema.example` nếu cần cấu hình địa chỉ API.

Admin phát triển dùng `admin@smartpark.local`; password nằm trong
`SMARTPARK_ADMIN_PASSWORD` ở file local. Setup không đổi password của Admin đã có.
Tài khoản trong database cũ không tự chuyển sang database mới.

Để gửi email OTP, thêm cấu hình SMTP thật theo `scripts/qa-local.example.json`
vào file local rồi chạy lại setup. SMS cần nhà cung cấp SMS; email onboarding
Operator cần `Onboarding:PublicOrigin` dùng HTTPS. Không commit cấu hình bí mật.

### Lỗi thường gặp

- `Configure Services:Key`: chạy lại setup và restart API trong Development.
- `Workflow delivery unavailable`: kiểm tra database tích hợp và
  `ConnectionStrings:User` trong User Secrets; setup chuẩn bị cả hai.
- Port 5035/5045/5055 bị chiếm: dừng instance API cũ trước khi chạy instance mới.

Chi tiết: [schema integration](docs/02-architecture/database/schema-integration.md),
[workflow migrations](docs/02-architecture/database/workflow-upgrades-2026-10-08.md).
