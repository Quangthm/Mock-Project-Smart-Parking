# Tích hợp database với code ứng dụng

Tài liệu này mô tả **profile đang triển khai** trên branch `feature/db-hybrid-srs-v1` (HEAD `b069e82`; branch point `0c8a6fb`; `develop` hiện tại `58df087`). Nguồn runtime là 7 base DDL + 8 overlay + `compose.schema-integration.yml` + `scripts/apply-workflow-migrations.ps1` + `setup.ps1`, không phải checkout arch.

## 1. Baseline và quan hệ với nhánh arch

- **Runtime canonical:** branch `feature/db-hybrid-srs-v1`. SQL nằm trong `scripts/database/microservices/` (7 file) và `scripts/database/overlay/` (8 file).
- **Design baseline:** `arch/database-schema` @ `9a13499` (SRS v1.0 RC3, commit `6165024`). Arch vẫn là nguồn đối chiếu thiết kế, nhưng **không dựng trực tiếp** để chạy BE (xem §2).
- **SRS:** `docs/01-requirements/srs/smartpark-srs-v1.0.md`.
- Các quyết định D1–D7/Q1–Q6 đã được phê tại cổng T4; bằng chứng nằm trong `docs/04-testing/reports/refactor-Docker Setup & Function Test/01-approved-decisions-D1-D7.md`.

## 2. Vì sao không chạy trực tiếp arch

Arch chỉ có 6 file base, không có `03a`/`03b`, không có overlay, và compose arch dùng một DB `smartpark_db`. Nếu chạy arch trực tiếp:

1. `setup.ps1`/`apply-workflow-migrations.ps1` fail vì thiếu `03a`/`03b` và overlay.
2. Overlay `parking-slot-constraints.sql` fail `42703` vì arch `parking_slots` không có `reservation_state`.
3. BE fail khi đọc/ghi `parking_slots.supported_vehicle_type`, `reservation_state`, `structure_backup_policies.unit_id/count`, và role `BUSINESS_OWNER`/`SITE_OPERATOR`/`PLATFORM_ADMIN`.
4. `tenants`, `structure_synchronizations`, `site_capacity_pools`, `structure_edit_holds`… không tồn tại ở DB đích.

Vì vậy branch giữ **arch làm thiết kế**, còn runtime dùng bộ base + overlay đã được điều chỉnh (D1–D7).

## 3. Topology 6 DB

| DB vật lý | Port | Base DDL | Overlay | Service dùng |
|---|---|---|---|---|
| `smartpark_user` | 5441 | `01-user-access-service-db.sql` | — | UserService |
| `smartpark_parking` | 5442 | `02-facility-service-db.sql`, `03a-parking-structure-ops-service-db.sql` | `parking-tenants`, `parking-structure-synchronizations`, `parking-operator-tables`, `parking-slot-constraints` | FacilityService |
| `smartpark_reservation` | 5443 | `03b-reservation-operations-service-db.sql` | `reservation-site-capacity-pools`, `reservation-structure-safety`, `reservation-structure-receipts`, `reservation-confirmation-time` | ReservationService |
| `smartpark_payment` | 5444 | `04-payment-pricing-service-db.sql` | — | PaymentService |
| `smartpark_notification` | 5445 | `05-notification-audit-service-db.sql` | — | NotificationService |
| `smartpark_iot` | 5446 | `06-iot-recognition-service-db.sql` | — | IoTService |

`03` là **một logical Operations service** nhưng tách 03a/03b theo DB đích (D1). Không có FK vật lý xuyên database; các tham chiếu là Logical ID.

## 4. Thứ tự áp dụng schema

`compose.schema-integration.yml` mount theo thứ tự số trong `docker-entrypoint-initdb.d`:

**Parking DB:** `01-base` (02) → `02-base` (03a) → `03-overlay-tenants` → `04-overlay-synchronizations` → `05-overlay-operator` → `06-overlay-slot-constraints`.

**Reservation DB:** `01-base` (03b) → `02-overlay-capacity-pools` → `03-overlay-structure-safety` → `04-overlay-receipts` → `05-overlay-confirmation`.

`reservation-site-capacity-pools.sql` phải chạy trước `reservation-structure-safety.sql` vì guard trigger tham chiếu `site_capacity_pools`.

Overlay dùng `CREATE TABLE IF NOT EXISTS` / `IF NOT EXISTS` / `NOT VALID` để chạy lại được trên DB dev. Base DDL là `CREATE TABLE`, không phải migration cho DB đã có dữ liệu: **không chạy base lên volume cũ**, không dùng EF `EnsureCreated` thay thế.

## 5. Chạy local

Yêu cầu: Docker Desktop, .NET 10 SDK, Node dependencies cho FE.

```powershell
# Lần đầu hoặc khi đổi schema layout: xoá volume và tạo lại 6 DB (destructive)
.\setup.ps1 -RecreateDb

# Các lần sau
.\setup.ps1
```

Các flag hữu ích: `-SkipDatabase` (không đụng Docker), `-SkipRestore`, `-SkipUserSecrets`, `-SkipSonarQube` (SonarQube mặc định chạy), `-WithAiCamera`, `-NonInteractive`, `-CredentialMode Team`.

`setup.ps1` thực hiện: validate `.env`/port → (tuỳ chọn) recreate volume → `docker compose -f compose.schema-integration.yml up -d` → chạy `scripts/apply-workflow-migrations.ps1` cho 6 service → probe bảng bắt buộc → tạo user-secrets `ConnectionStrings:User/Parking/Reservation` → build/restore → (tuỳ chọn) SonarQube/AI.

Sau khi setup, chạy 3 service tích hợp:

```powershell
./scripts/run-schema-service.ps1 -Service User        # API 5035 → DB 5441
./scripts/run-schema-service.ps1 -Service Parking     # API 5045 → DB 5442
./scripts/run-schema-service.ps1 -Service Reservation # API 5055 → DB 5443
```

`run-schema-service.ps1` đọc `.env`, đặt `ConnectionStrings__<Service>` đúng DB/port và `Services__Key`. Payment/Notification/IoT chưa có runner tương ứng trong phạm vi tích hợp này.

## 6. Demo data và role

`setup.ps1` tạo Admin qua `DevelopmentAdmin` (BCrypt, chỉ Development). Các role seed trong `01` gồm 7 mã: 4 mã actor SRS (`DRIVER`, `OWNER`, `OPERATOR`, `ADMIN`) + 3 mã runtime (`BUSINESS_OWNER`, `SITE_OPERATOR`, `PLATFORM_ADMIN`) do UserService dùng. Dev có thể seed demo Owner/Operator qua `DevelopmentOwner`/`DevelopmentOperator`; sau khi UserService chạy lần đầu, `scripts/seed-demo-site.ps1` tạo tenant/site/slot/grant demo idempotent để test ngay. Production phải có seed/role migration tương ứng trước khi dùng các luồng đó.

## 7. Fence và phục hồi lỗi

Parking mutation lấy hold tại Reservation, commit transaction Parking, rồi publish site state/tombstones/capacity snapshot trước khi release. Capacity pool có counter 0 không được coi là commitment chỉ vì `total_capacity > 0`. Site inactive chặn commitment mới; quota giữ nguyên khi đóng/mở.

Nếu timeout/restart sau acquire hoặc trong commit/release: giữ hold, không tự xóa. Dừng **tất cả** Parking API, giữ Reservation API + DB chạy, dùng `scripts/recover-structure-hold.ps1` (inspect-only mặc định, `-Apply` khi snapshot khớp) rồi mới restart Parking. Đây chưa phải distributed transaction/outbox tự phục hồi.

## 8. Deviations D1–D7

| Mã | Nội dung |
|---|---|
| D1 | 6 DB BE-first; tách 03a/03b |
| D2 | Giữ `uq_active_vehicle_plate`; BE map `23505` → 409 |
| D3/D6 | Operator tables + BE-shape `support_tickets` trong parking overlay |
| D4 | 7 role seeds; 2 cột `parking_slots`; rename `unit_id`/`count` |
| D5 | `tenants` canonical ở `01`, bản copy ở parking |
| D7 | Recreate `site_capacity_pools` ở reservation overlay |

Chi tiết: report 01 và report 10 trong `docs/04-testing/reports/refactor-Docker Setup & Function Test/`.

## 9. Kiểm chứng

- `docker compose -f compose.schema-integration.yml config` — validate 6 service/15 mount.
- `scripts/apply-workflow-migrations.ps1` — pre-check bảng theo DB, block-list script legacy `05.1–05.9`/`integration/01–03`.
- `setup.ps1 -RecreateDb` + probe 6 DB — đường chạy T6; xem report 07 và checklist T6 (chưa có evidence chạy đầy đủ trong repo).
- BE tests: `dotnet test SmartParking.slnx` (DB tests cần `SMARTPARK_AUTH_TEST_CONNECTION` trỏ PostgreSQL test có quyền tạo/xóa database test UUID riêng).
- FE: `npm run build`.

Kết quả/evidence mới nhất nằm trong report 07, 08, 09 và 10; không dùng lại con số 74/74 của baseline 2026-10-07 cho profile 6-DB hiện tại.

## 10. Giới hạn

- RLS chưa bật; chỉ denormalize `tenant_id` và validate ở application.
- Payment/Notification/IoT chưa có runner local đầy đủ trong tài liệu này.
- `support_tickets` BE-shape ở parking là technical debt có chủ đích (D6).
- Các script legacy `05.1–05.9` và `integration/01–03` bị chặn; không mount lại.
