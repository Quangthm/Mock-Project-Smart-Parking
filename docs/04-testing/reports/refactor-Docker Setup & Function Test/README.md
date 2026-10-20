# Reports — Refactor DB / Docker & Function Test (2026-10-18)

Thư mục bằng chứng (PM evidence) cho đợt chuẩn hoá DB Hybrid theo SRS v1.0 trên branch `feature/db-hybrid-srs-v1` (base `develop` @ `0c8a6fb`).

## Danh mục report

| # | File | Nội dung (1 dòng) | Lane |
|---|---|---|---|
| 01 | `01-approved-decisions-D1-D7.md` | Quyết định đã duyệt D1–D7 / Q1–Q6 (bảng map 6 DB, plate index, operator tables, base-file deviations). | SQL |
| 02 | `02-sql-base-files-microservices.md` | SQL base files: 7 file `microservices/*` (user-access, facility, 03a/03b, payment-pricing, notification-audit, iot-recognition). | SQL |
| 03 | `03-sql-parking-overlays.md` | Parking overlays: `parking-tenants`, `parking-structure-synchronizations`, `parking-operator-tables`, `parking-slot-constraints`. | SQL |
| 04 | `04-sql-reservation-overlays.md` | Reservation overlays: `reservation-site-capacity-pools`, `reservation-structure-safety`, `reservation-structure-receipts`, `reservation-confirmation-time`. | SQL |
| 05 | `05-compose-schema-integration-6db.md` | `compose.schema-integration.yml`: 6 DB services, 15 mount với target tiền tố số ép thứ tự init, 15/15 source path Test-Path OK, không còn mount file đã xoá / `05.x` / `integration/*`. | Infra |
| 06 | `06-apply-workflow-migrations.md` | `scripts/apply-workflow-migrations.ps1`: `ValidateSet` 6 service, mảng script theo DB khớp compose, block-list guard `05.1–05.9` + `integration/01–03`, pre-check bảng theo DB. | Infra |
| 07 | `07-setup-ps1-recreatedb.md` | `setup.ps1`: `-RecreateDb` + `down -v`, vòng validate 6 port, vòng migration/probe 6 service, password guard downgrade thành warning khi `-RecreateDb`, user-secrets vẫn 3 service. | Infra |
| 08 | `08-vehicle-registration-plate-409.md` | `VehicleRegistrationService.cs`: bắt `DbException` `SqlState 23505` → `AuthException("PLATE_EXISTS", …, 409)` cho plate active trùng (D2a). | BE-handler |
| 09 | `09-setup-ps1-sonarqube-ai-dev.md` | `setup.ps1`: SonarQube mặc định, AI Camera opt-in, dev container, seed user OWNER/OPERATOR và các fix runtime P0. | Infra |
| 10 | `10-db-docs-deployment-alignment.md` | Đối chiếu docs DB arch vs profile 6-DB đang triển khai; phương án đã chọn (giữ branch runtime canonical, cập nhật docs) + evidence PM. | Docs |

> **01–04** do lane SQL viết (không thuộc lane tài liệu này); tên file chính xác do lane SQL sở hữu. Report 05–09 do lane infra/BE-handler thực hiện; report 10 do lane Docs thực hiện và được ghi nhận tại đây.

## Timeline T1–T6
- **T1** — Tạo branch `feature/db-hybrid-srs-v1` từ `develop` @ `0c8a6fb` (commit đầu = y nguyên develop).
- **T2** — Copy 6 SQL chuẩn từ worktree arch `arch/database-schema` @ `9a13499` vào `scripts/database/microservices/`, hash-verified.
- **T3** — Phân tích impact + standardization plan (`T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md`).
- **T4** — Phê duyệt quyết định **D1–D7** và **Q1–Q6** (gate).
- **T5** — Triển khai: SQL base + overlay (lane SQL), compose/runner/setup 6-DB (lane infra), BE handler `23505→409` (lane BE-handler).
  - *Ghi chú quy trình (minh bạch):* lane infra lần 1 báo thành công nhưng **write không persist xuống disk**; một lane retry đã ghi lại và **verify độc lập 15/15 mount path + PS syntax clean**.
- **T6** — Người dùng chạy `setup.ps1 -RecreateDb` + test BE theo checklist `D:\My file\FA26 - OJT FSoft\T6-Checklist-BE-Test-2026-10-18.md`.

## Lệnh tái kiểm chứng chung
```powershell
# xem toàn bộ thay đổi so với base
git diff 0c8a6fb -- compose.schema-integration.yml scripts/apply-workflow-migrations.ps1 setup.ps1
# validate compose (không cần up)
docker compose -f compose.schema-integration.yml config
# syntax check 2 script PowerShell
$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path setup.ps1),[ref]$t,[ref]$e); $e.Count
# chạy recreate + init 6 DB (destructive — xoá dev data)
.\setup.ps1 -RecreateDb
```

## Phạm vi (scope note)
Working tree còn chứa các file **local có sẵn, không thuộc change set này**: `ai/*`, `docs/03-design/3d-digital-twin/*`, `setup_fe-v2.1.ps1`, và file rời `11`. Các file này **không** nằm trong phạm vi refactor DB hybrid và không được tính là evidence của T5.
