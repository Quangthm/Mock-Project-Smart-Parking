# 06 — scripts/apply-workflow-migrations.ps1: runner 6 DB + block-list

## Meta
- **Ngày:** 2026-10-18
- **Branch:** `feature/db-hybrid-srs-v1`
- **Base commit:** `develop` @ `0c8a6fb`
- **File:** `scripts/apply-workflow-migrations.ps1`
- **Lane:** T5 infra (migration runner)

## Thay đổi chính
- `ValidateSet` của tham số `-Service` mở rộng từ 3 lên **6 giá trị**: `User`, `Parking`, `Reservation`, `Payment`, `Notification`, `Iot` (dòng 2).
- Thay map 1-file-per-service bằng **mảng script có thứ tự theo từng DB**, phản chiếu đúng thứ tự mount của `compose.schema-integration.yml` (dòng 18–46):
  - `User`: `microservices/01-user-access-service-db.sql`
  - `Parking`: `02-facility` → `03a-parking-structure-ops` → `overlay/parking-tenants` → `overlay/parking-structure-synchronizations` → `overlay/parking-operator-tables` → `overlay/parking-slot-constraints`
  - `Reservation`: `03b-reservation-operations` → `overlay/reservation-site-capacity-pools` → `overlay/reservation-structure-safety` → `overlay/reservation-structure-receipts` → `overlay/reservation-confirmation-time`
  - `Payment`: `04-payment-pricing`; `Notification`: `05-notification-audit`; `Iot`: `06-iot-recognition`
- Thêm **block-list guard** (dòng 59–64): throw nếu đường dẫn khớp `05.1–05.9` hoặc `integration/01`, `integration/02*`, `integration/03*` — nội dung các file này đã bị thay thế bởi base files + overlay.
- Thêm **pre-check tồn tại file** trước khi apply (dòng 65–67).
- **Pre-check bảng** theo từng DB trước khi apply (dòng 48–57):
  - `User`: `users`, `accounts`, `roles`, `vehicles`
  - `Parking`: `parking_sites`, `tenants`, `structure_synchronizations`, `support_tickets`, `operator_audit_logs`
  - `Reservation`: `reservations`, `structure_edit_holds`, `site_capacity_pools`, `structure_release_receipts`
  - `Payment` / `Notification` / `Iot`: rỗng (chưa có BE service)
- Giữ nguyên cơ chế kết nối/psql: `docker compose --project-name … --env-file … -f … exec -T <container> psql -U <db> -d <db> -v ON_ERROR_STOP=1`, đọc SQL bằng `Get-Content -Raw` rồi pipe qua stdin.

## Lý do / quyết định liên quan
- **D1 (T4):** runner phải phục vụ đủ 6 DB vật lý, tên container `$service-db` khớp compose.
- **Risk #2 (plan):** overlay `05.1–05.8` cũ nếu apply trùng → regression (permissions, plate index) ⇒ block-list chặn cứng.
- **Q2/Q3 (T4):** overlay bắt buộc giữ (`integration/03`, `05.9–05.12`) đã được port vào base/overlay mới nên runner chỉ apply bộ file mới.

## Evidence
`git diff 0c8a6fb -- scripts/apply-workflow-migrations.ps1` (trích, đã lược):

```diff
-    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation')][string]$Service,
+    [Parameter(Mandatory)][ValidateSet('User','Parking','Reservation','Payment','Notification','Iot')][string]$Service,
…
-$database = @{ User='smartpark_user'; Parking='smartpark_parking'; Reservation='smartpark_reservation' }[$Service]
-$sql = Get-Content -LiteralPath (Join-Path $codeRoot "scripts/database/$migration") -Raw
-$sql | & docker compose … exec -T $container psql …
-if ($Service -in @('Parking','Reservation')) { … 05.11 / 05.12 … }
+$scripts = @{ User = @(…); Parking = @(…6 file…); Reservation = @(…5 file…); Payment = @(…); Notification = @(…); Iot = @(…) }[$Service]
+$blockedPattern = '(^|[\\/])05\.[1-9][^\\/]*\.sql$|(^|[\\/])integration[\\/]0[123][^\\/]*\.sql$'
+foreach ($script in $scripts) {
+    if ($relative -match $blockedPattern) { throw "Blocked migration script '$relative': …" }
+    if (!(Test-Path -LiteralPath (Join-Path $codeRoot $relative) -PathType Leaf)) { throw "Migration script not found: $relative." }
+}
+foreach ($script in $scripts) { $sql = Get-Content …; $sql | & docker compose … psql … }
```

Line refs hiện tại (trước cập nhật bên dưới): `ValidateSet` dòng 2; mảng `$scripts` dòng 18–46; `$required` dòng 48–57; `$blockedPattern` dòng 59; guard dòng 60–67; vòng apply dòng 75–79.

## Ảnh hưởng
- `setup.ps1` gọi runner cho cả 6 service (xem report 07) — runner chấp nhận đủ 6 giá trị.
- Apply idempotent theo thứ tự base→overlay; file legacy bị chặn sớm với thông báo rõ ràng thay vì lỗi SQL khó hiểu.
- `Payment`/`Notification`/`Iot` không có pre-check bảng (DB mới, chỉ chạy base).

## Cách tái kiểm chứng
```powershell
git diff 0c8a6fb -- scripts/apply-workflow-migrations.ps1
# syntax check
$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/apply-workflow-migrations.ps1),[ref]$t,[ref]$e); $e.Count
# chạy thử 1 DB (sau khi DB đã up)
.\scripts\apply-workflow-migrations.ps1 -Service Parking -SchemaRoot . -EnvPath .\.env
```

## Cập nhật 2026-10-18 (sau lần chạy T6 thực tế)

Lần chạy `.\setup.ps1` đầu tiên trên volume mới **fail**: docker init đã chạy đủ 15 script SQL (6 DB Healthy), rồi runner applied lại cùng các base file → `ERROR: relation "users" already exists` tại dòng con `[1/6] Probing... User`. Runner cũ luôn apply và pre-check bảng *trước* apply — volume mới đã có bảng nên pre-check pass rồi apply trùng.

Sửa cùng ngày — runner probe trạng thái DB trước khi áp script, using sentinel quan hệ trong `pg_class` (dòng 58):
- `current` (sentinel có mặt) → skip apply, chỉ verify bảng bắt buộc (dòng 94–98);
- `empty` (không bảng public nào) → áp trọn bộ canonical theo mảng script (dòng 85–92);
- `legacy` (có bảng cũ, thiếu sentinel) → throw yêu cầu `.\setup.ps1 -RecreateDb` (dòng 81–83).

Sentinel: User = index `uq_active_vehicle_plate`; Parking = `tenants`; Reservation = `site_capacity_pools`; Payment = `payment_orders`; Notification = `notification_templates`; Iot = `devices`. Verify bắt buộc chạy ở cả hai nhánh current/empty (`$required` cho User/Parking/Reservation; chính sentinel cho Payment/Notification/Iot). Block-list, mảng script và cơ chế psql/exec giữ nguyên.

Đồng bộ tiếp theo: `scripts/test-workflow-upgrade.ps1` viết lại theo fixture DB trống (lần 1 apply, lần 2 skip, restart giữ dữ liệu); QUICK_START mục "Nhật ký lỗi đã biết" + LOCAL-SETUP volume cũ → `-RecreateDb`; `.env.example`/`setup.ps1` thêm URL AI (REST 5001 / live 5002) vào FE/.env khi trống.

Line refs mới: `$sentinel` 58; `$blockedPattern` 62; probe 73–79; legacy 81–83; empty-apply 85–92; verify 94–98; output 100–103.
