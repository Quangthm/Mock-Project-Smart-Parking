# 03 — Overlay SQL cho `smartpark_parking` (4 file)

## Meta

| Hạt | Giá trị |
|---|---|
| Ngày | 2026-10-18 |
| Branch | `feature/db-hybrid-srs-v1` |
| Base commit | `0c8a6fb` (= `develop`) |
| Nguồn arch | worktree A @ `9a13499` (SRS v1.0 SQL source — arch **không** có các overlay này) |
| Thư mục | `scripts/database/overlay/` (trạng thái git: `??` — untracked, chưa commit) |
| DB đích | `smartpark_parking` (container `parking-db`, port `${SMARTPARK_DB_PARKING_PORT:-5442}`) |
| Thành phần | 4 file: `parking-tenants.sql` (18 dòng), `parking-structure-synchronizations.sql` (19 dòng), `parking-operator-tables.sql` (50 dòng), `parking-slot-constraints.sql` (13 dòng) |

---

## Thay đổi chính

### A. `parking-tenants.sql` — bản duplicate của `tenants` trong parking DB

| Trường | Nội dung |
|---|---|
| Object tạo | `CREATE EXTENSION "uuid-ossp"` (`:4`); bảng `tenants` (`:5-16`, PK `id`, CHECK `status IN ('ACTIVE','INACTIVE','SUSPENDED')`); unique index `uq_active_tenant_code` trên `(code) WHERE deleted_at IS NULL` (`:17`); index `idx_tenants_status` (`:18`) |
| Purpose | ParkingService JOIN `tenants` với `parking_sites` trên **một** connection (`ConnectionStrings:Parking`) — nếu `tenants` chỉ nằm ở `smartpark_user` thì mọi JOIN/`SELECT FOR SHARE` của ParkingService vỡ (gap P4, quyết định Q3/D5) |
| FK | **Không có FK** tới bất kỳ bảng nào (đặc biệt không FK ra ngoài — bảng là self-contained copy) |

### B. `parking-structure-synchronizations.sql` — bảng sync cho structure worker

| Trường | Nội dung |
|---|---|
| Object tạo | Bảng `structure_synchronizations` (`:6-15`: PK `token`, `state` CHECK `preparing/aborting/ready/completed`, CHECK `state <> 'ready' OR outcome IS NOT NULL`, FK `site_id → parking_sites(id)` `:9`, FK `operation_id → structure_operations(id)` `:10`); partial index `ix_structure_synchronizations_pending` trên `created_at WHERE state <> 'completed'` (`:17`) |
| Purpose | Worker `StructureSynchronizationWorker` (ParkingService, chạy nền ~10s) đọc/ghi bảng này — arch `9a13499` không tạo bảng này, chỉ còn script legacy `05.11` (bị runner chặn) → gap P3; shape lấy từ `05.11` nhưng viết lại `IF NOT EXISTS` và FK **trong cùng DB** với `parking_sites` + `structure_operations` (đều do `02`/`03a` tạo trong `smartpark_parking`) |
| FK | FK tới `parking_sites`, `structure_operations` (cùng DB) — **không có FK tới `tenants`** |

### C. `parking-operator-tables.sql` — 3 bảng operator shape-BE (D3(a)/D6)

| Trường | Nội dung |
|---|---|
| Object tạo | `support_tickets` (`:6-17`: `user_id`, `subject`, `message`, CHECK `status IN ('open','in-progress','resolved')`) + index `idx_support_tickets_site_created` (`:19-20`); `operator_notifications` (`:22-33`) + index `idx_operator_notifications_recipient` (`:34-35`); `operator_audit_logs` (`:37-48`) + index `idx_operator_audit_logs_site_created` (`:49-50`); `CREATE EXTENSION "uuid-ossp"` (`:4`) |
| Purpose | Bật 4 endpoint nhóm `/api/operator/...` (`OperatorOperationsEndpoints.cs`: GET/POST/PATCH tickets, GET/POST incidents …) vốn trả **503 `PROTOTYPE_SCHEMA_NOT_READY`** trên DB fresh vì `EnsureSchemaAsync` là dead-code và không script nào tạo bảng (Q5/P5/P6). `operator_incident_events` **cố ý không tạo ở đây** — đã có trong `03a:24-34` (ghi chú dòng 2 của file) → tổng 4/4 bảng. `support_tickets` theo **shape BE** tạm (D6), khác shape SRS ở `05-notification-audit:63` |
| FK | **Không có FK** (toàn bộ chỉ `UUID … NOT NULL`, không `REFERENCES`) — đặc biệt **không FK tới `tenants`** dù có cột `tenant_id` |

### D. `parking-slot-constraints.sql` — constraint/index cho `parking_slots`

| Trường | Nội dung |
|---|---|
| Object tạo | Constraint `ck_deleted_slot_safe` trên `parking_slots` (`:7-9`: `deleted_at IS NULL OR (physical_state NOT IN ('OCCUPIED','UNKNOWN') AND reservation_state IS NULL)` — `NOT VALID`); unique index `uq_site_slot_code_ci` trên `(tenant_id, site_id, upper(slot_code))` (`:12`) |
| Purpose | Port **một phần** của `integration/02` (2 object được phép giữ theo T3 §3.5). Constraint đọc cột **`reservation_state`** — cột này chỉ tồn tại nhờ D4 (được thêm ở `02-facility-service-db.sql:80`); do đó overlay này phải chạy **sau** file base `02`. Chú ý index này khác với `uq_active_slot_code_ci` của base `02:88` (base index theo `spatial_unit_id`, partial `WHERE deleted_at IS NULL`; overlay index theo `tenant_id, site_id`, không partial) |
| FK | **Không có FK**; không tham chiếu `tenants` (chỉ dùng cột `tenant_id` nằm sẵn trong `parking_slots`) |

**Ghi chú chung về FK tới `tenants`:** lệnh `Select-String -Path scripts\database\overlay\*.sql -Pattern 'REFERENCES'` chỉ trả về 2 dòng — `parking-structure-synchronizations.sql:9-10` (`parking_sites`, `structure_operations`). Không overlay nào (parking lẫn reservation) có FK tới `tenants` → 2 bản `tenants` (01 + overlay) độc lập, đúng thiết kế logical-ID.

### E. Thứ tự apply trên `parking-db`

Compose (`compose.schema-integration.yml`) mount vào `/docker-entrypoint-initdb.d` — tên file số đảm bảo thứ tự:

| Thứ tự | Mount (compose line) | Container name |
|---|---|---|
| 1 | `microservices/02-facility-service-db.sql` → `01-base.sql` (`:27`) | base |
| 2 | `microservices/03a-parking-structure-ops-service-db.sql` → `02-base.sql` (`:28`) | base |
| 3 | `overlay/parking-tenants.sql` → **`03-overlay-tenants.sql`** (`:29`) | overlay 1 |
| 4 | `overlay/parking-structure-synchronizations.sql` → **`04-overlay-synchronizations.sql`** (`:30`) | overlay 2 |
| 5 | `overlay/parking-operator-tables.sql` → **`05-overlay-operator.sql`** (`:31`) | overlay 3 |
| 6 | `overlay/parking-slot-constraints.sql` → **`06-overlay-slot-constraints.sql`** (`:32`) | overlay 4 |

Thứ tự tương tự được mirror trong runner `scripts/apply-workflow-migrations.ps1:21-28` (`Parking = @(02, 03a, tenants, synchronizations, operator, slot-constraints)`) — mỗi script chạy qua `psql -v ON_ERROR_STOP=1`.

---

## Lý do / quyết định liên quan

- **Q3 / D5** → `parking-tenants.sql`: mọi truy cập `tenants` của BE dùng `ConnectionStrings:Parking`.
- **Q2 / P3** → `parking-structure-synchronizations.sql`: giữ cấu trúc structure-sync của develop dưới dạng overlay mới (arch không có bảng này).
- **Q5 / D3 / D6 / P5 / P6** → `parking-operator-tables.sql`: port DDL shape-BE để gỡ 503; `support_tickets` shape-BE tạm.
- **P1 + T3 §3.3 dòng 77** → `parking-slot-constraints.sql`: giữ đúng 2 object được phê của `integration/02`, chạy sau khi base `02` đã có cột `reservation_state`.
- **T3 §2.1 (gap Parking)** là danh sách gap mà 4 overlay này đóng góp giải quyết cùng `02`/`03a` base.

---

## Evidence

### E1. Danh sách object (grep file hiện hành, file:line)

```
$ Select-String -Path scripts\database\overlay\*.sql -Pattern 'CREATE TABLE|CREATE UNIQUE INDEX|CREATE INDEX|ADD CONSTRAINT'
parking-tenants.sql:5:                        CREATE TABLE IF NOT EXISTS tenants (
parking-tenants.sql:17:                       CREATE UNIQUE INDEX IF NOT EXISTS uq_active_tenant_code ON tenants (code) WHERE deleted_at IS NULL;
parking-tenants.sql:18:                       CREATE INDEX IF NOT EXISTS idx_tenants_status ON tenants (status);
parking-structure-synchronizations.sql:6:     CREATE TABLE IF NOT EXISTS structure_synchronizations (
parking-structure-synchronizations.sql:17:    CREATE INDEX IF NOT EXISTS ix_structure_synchronizations_pending …
parking-operator-tables.sql:6:                CREATE TABLE IF NOT EXISTS support_tickets (
parking-operator-tables.sql:19:               CREATE INDEX IF NOT EXISTS idx_support_tickets_site_created …
parking-operator-tables.sql:22:               CREATE TABLE IF NOT EXISTS operator_notifications (
parking-operator-tables.sql:34:               CREATE INDEX IF NOT EXISTS idx_operator_notifications_recipient …
parking-operator-tables.sql:37:               CREATE TABLE IF NOT EXISTS operator_audit_logs (
parking-operator-tables.sql:49:               CREATE INDEX IF NOT EXISTS idx_operator_audit_logs_site_created …
parking-slot-constraints.sql:7:               ALTER TABLE parking_slots ADD CONSTRAINT ck_deleted_slot_safe …
parking-slot-constraints.sql:12:              CREATE UNIQUE INDEX IF NOT EXISTS uq_site_slot_code_ci ON parking_slots(tenant_id, site_id, upper(slot_code));
```

### E2. Không có FK tới `tenants` (grep `REFERENCES` toàn bộ overlay)

```
$ Select-String -Path scripts\database\overlay\*.sql -Pattern 'REFERENCES'
parking-structure-synchronizations.sql:9:     site_id UUID NOT NULL REFERENCES parking_sites(id),
parking-structure-synchronizations.sql:10:    operation_id UUID REFERENCES structure_operations(id),
(2 kết quả — không dòng nào tham chiếu `tenants`)
```

### E3. Thứ tự mount compose (nguyên văn, cắt …)

```
$ git diff -- compose.schema-integration.yml        # (trích khối parking-db)
@@ -26,10 +24,12 @@ services:
     volumes:
       - parking-schema-data:/var/lib/postgresql/data
-      - ./scripts/database/microservices/02-parking-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
-      - ./scripts/database/integration/02-parking-service-alignment.sql:/docker-entrypoint-initdb.d/02-alignment.sql:ro
-      - ./scripts/database/05.9-Parking-Backup-Operations.sql:/docker-entrypoint-initdb.d/03-workflows.sql:ro
-      - ./scripts/database/05.11-Parking-Structure-Synchronization.sql:/docker-entrypoint-initdb.d/04-synchronization.sql:ro
+      - ./scripts/database/microservices/02-facility-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
+      - ./scripts/database/microservices/03a-parking-structure-ops-service-db.sql:/docker-entrypoint-initdb.d/02-base.sql:ro
+      - ./scripts/database/overlay/parking-tenants.sql:/docker-entrypoint-initdb.d/03-overlay-tenants.sql:ro
+      - ./scripts/database/overlay/parking-structure-synchronizations.sql:/docker-entrypoint-initdb.d/04-overlay-synchronizations.sql:ro
+      - ./scripts/database/overlay/parking-operator-tables.sql:/docker-entrypoint-initdb.d/05-overlay-operator.sql:ro
+      - ./scripts/database/overlay/parking-slot-constraints.sql:/docker-entrypoint-initdb.d/06-overlay-slot-constraints.sql:ro
```

```
$ scripts/apply-workflow-migrations.ps1:21-28
    Parking = @(
        'microservices/02-facility-service-db.sql',
        'microservices/03a-parking-structure-ops-service-db.sql',
        'overlay/parking-tenants.sql',
        'overlay/parking-structure-synchronizations.sql',
        'overlay/parking-operator-tables.sql',
        'overlay/parking-slot-constraints.sql'
    )
```

### E4. Bằng chứng purpose (grep BE + comment file)

```
parking-operator-tables.sql:1-3 (comment):
-- Overlay: operator tables with exact runtime DDL shapes from OperatorOperationsStore.EnsureSchemaAsync
-- NOTE: operator_incident_events is intentionally omitted — provided by 03a
-- Purpose: T4-approved D3(a) — BE-shape prototype schema made durable (P5/P6)

src/Services/ParkingService/SmartParking.ParkingService.API/Program.cs:56,60:
    context.Request.Path.StartsWithSegments("/api/operator") && …
    … new { success = false, code = "PROTOTYPE_SCHEMA_NOT_READY", … }

src/Services/ParkingService/SmartParking.ParkingService.API/OperatorOperationsEndpoints.cs:10,34,53,62:
    app.MapGet("/api/operator/sites/{siteId:guid}/tickets", …) · MapPatch …/tickets/{ticketId:guid} · MapGet …/incidents · MapPost …/incidents
```

---

## Ảnh hưởng

- **Endpoints được khôi phục:** nhóm `/api/operator/*` (tickets/incidents/audit/notifications) hết 503 trên DB fresh; 2 index `idx_support_tickets_site_created`, `idx_operator_*_recipient/site` phục vụ query mặc định của `OperatorOperationsStore`.
- **Không cải tiến join vật lý:** vì overlay **không** FK sang `tenants`, tính toàn vẹn quan hệ `tenant_id` chỉ mang tính logical (đúng triết lý cross-domain của SRS) — migration data phải tự bảo đảm `tenant_id` hợp lệ.
- **Phụ thuộc thứ tự:** `parking-slot-constraints.sql` đọc cột `reservation_state` → phải chạy sau base `02` (đã đúng trong compose/runner); nếu chạy trên DB chưa có cột → lỗi `42703`.
- **Rủi ro migration lại (re-run):** toàn bộ dùng `IF NOT EXISTS`/guard `pg_constraint` → idempotent; riêng `CREATE EXTENSION` đã `IF NOT EXISTS`.
- **`support_tickets` là bản tạm (D6):** shape SRS nằm ở `05-notification-audit` chưa dùng — technical debt có chủ đích.

---

## Cách tái kiểm chứng

```powershell
# 1) File tồn tại + thứ tự mount (chỉ đọc)
Get-ChildItem scripts\database\overlay -Filter 'parking-*.sql' | Select-Object Name, Length
Select-String -Path compose.schema-integration.yml -Pattern 'overlay/parking'

# 2) Object đã tạo + không có FK tới tenants
Select-String -Path scripts\database\overlay\parking-*.sql -Pattern 'CREATE TABLE|CREATE INDEX|ADD CONSTRAINT'
Select-String -Path scripts\database\overlay\parking-*.sql -Pattern 'REFERENCES\s+tenants'   # -> 0 dòng

# 3) Sau khi chạy setup/compose (kiểm tra runtime — psql đọc, không ghi schema)
docker compose --project-name smartpark-schema -f compose.schema-integration.yml exec -T parking-db `
  psql -U smartpark_parking -d smartpark_parking -At -c `
  "SELECT table_name FROM information_schema.tables WHERE table_name IN ('tenants','structure_synchronizations','support_tickets','operator_notifications','operator_audit_logs','operator_incident_events') ORDER BY 1;"
# -> 6 dòng: operator_audit_logs, operator_incident_events, operator_notifications, structure_synchronizations, support_tickets, tenants

docker compose --project-name smartpark-schema -f compose.schema-integration.yml exec -T parking-db `
  psql -U smartpark_parking -d smartpark_parking -At -c `
  "SELECT conname FROM pg_constraint WHERE conrelid='parking_slots'::regclass AND conname='ck_deleted_slot_safe';"
# -> ck_deleted_slot_safe

# 4) Gap 503 đã được xử lý (đọc code)
Select-String -Path src\Services\ParkingService\SmartParking.ParkingService.API\Program.cs -Pattern 'PROTOTYPE_SCHEMA_NOT_READY'
```
