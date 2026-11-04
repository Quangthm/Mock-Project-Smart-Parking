# SmartPark — Hệ thống quản lý bãi đỗ xe thông minh

SmartPark là hệ thống quản lý bãi đỗ xe gồm backend microservice .NET, frontend React (có phần 3D/digital twin), và bộ cơ sở dữ liệu PostgreSQL theo từng service.

## Thành phần chính

| Khu vực | Nội dung |
| :--- | :--- |
| Backend | 5 microservice .NET trong `src/Services/`: `UserService`, `FacilityService`, `OperationService`, `PaymentService`, `IoTService` |
| Frontend | React (Vite) trong `FE/`, gồm cả chế độ xem 2D/3D bãi xe |
| Cơ sở dữ liệu | 6 database PostgreSQL, khởi tạo từ `scripts/database/microservices/` qua `compose.schema-integration.yml` |
| Messaging | RabbitMQ + MassTransit (`compose.schema-integration.yml` cung cấp container `smartpark-rabbitmq`) |

## Bắt đầu nhanh

```powershell
# 1. Tạo file cấu hình môi trường và điền mật khẩu dev
Copy-Item .env.example .env

# 2. Khởi tạo schema + build (chạy một lần)
.\setup.ps1

# 3. Chạy stack ứng dụng
.\scripts\run-app-service.ps1 -Service All

# 4. Nạp dữ liệu demo (tùy chọn)
.\scripts\seed-demo-site.ps1

# 5. Dừng stack
.\scripts\stop-app-service.ps1 -Service All
```

- Hướng dẫn chi tiết theo từng bước: [LOCAL-SETUP.md](LOCAL-SETUP.md)
- Bản rút gọn: [QUICK_START.md](QUICK_START.md)
- Chạy test PostgreSQL bằng Visual Studio hoặc terminal: [tests/README.md](tests/README.md)

> **Cảnh báo:** `setup.ps1` có nhánh reset chạy `docker compose ... down -v`, tức là xoá volume của cả 6 database. Đọc kỹ trước khi chạy trên môi trường đang có dữ liệu cần giữ.

## Kiểm thử

```powershell
dotnet build SmartParking.slnx
dotnet test SmartParking.slnx
```

- Bộ test backend nằm trong `tests/Services/`.
- Một số test tích hợp cần PostgreSQL đang chạy và connection string qua biến môi trường (xem `tests/README.md` và `.\scripts\configure-test-settings.ps1`).

## Tài liệu

Bắt đầu từ chỉ mục [docs/README.md](docs/README.md).

| Khu vực | Nội dung |
| :--- | :--- |
| `docs/01-requirements/` | SRS, business rules, use cases, traceability (baseline v1.1) |
| `docs/02-architecture/` | Kiến trúc service, hợp đồng API, thiết kế database |
| `docs/03-design/api/` | Thiết kế API theo domain — endpoint mới phải theo `api-design-template.md` |
| `docs/04-testing/` | Kế hoạch test, test case, Postman collection, báo cáo |
| `docs/05-project-management/` | Decision log, handoff, thay đổi và validation |

## Quy ước cho AI agent

- [AGENTS.md](AGENTS.md) là điểm vào, trỏ tới router chuẩn [ai/AGENTS.md](ai/AGENTS.md).
- Mọi task code/review phải nạp `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` theo router.

## Đóng góp và giấy phép

- Quy ước đóng góp: [CONTRIBUTING.md](CONTRIBUTING.md)
- Giấy phép: [LICENSE](LICENSE)
