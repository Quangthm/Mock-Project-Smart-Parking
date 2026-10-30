# 04 — Overlay SQL cho `smartpark_reservation` (4 file)

## Meta

| Hạt | Giá trị |
|---|---|
| Ngày | 2026-10-18 |
| Branch | `feature/db-hybrid-srs-v1` |
| Base commit | `0c8a6fb` (= `develop`) |
| Nguồn arch | worktree A @ `9a13499` (SRS v1.0 — arch **không** có các object này; nguồn tái tạo = script develop `integration/03`, `05.10`, `05.12` và file `03` cũ đã xóa) |
| Thư mục | `scripts/database/overlay/` (trạng thái git: `??` — untracked, chưa commit) |
| DB đích | `smartpark_reservation` (container `reservation-db`, port `${SMARTPARK_DB_RESERVATION_PORT:-5443}`) |
| Thành phần | 4 file: `reservation-site-capacity-pools.sql` (42 dòng), `reservation-structure-safety.sql` (69 dòng), `reservation-structure-receipts.sql` (22 dòng), `reservation-confirmation-time.sql` (33 dòng) |

---

## Thay đổi chính

### A. `reservation-site-capacity-pools.sql` — tái tạo `site_capacity_pools` shape-BE (overlay 1/4)

| Trường | Nội dung |
|---|---|
| Object tạo | Bảng `site_capacity_pools` (`:19-38`: PK `id`, `tenant_id`, `site_id`, `spatial_unit_id`, CHECK `vehicle_type IN ('CAR','MOTORCYCLE')`, `total_capacity`, `max_reservation_quota`, `current_reserved_count`, `pending_payment_count`, `occupied_count`, `protected_count`, `backup_count`, `updated_at`, `UNIQUE (id, tenant_id, site_id)`, 2 CHECK capacity); index `idx_site_capacity_pools_tenant_site` (`:40`) |
| Purpose | Gap **R3**: file `03` cũ (đã xóa) từng có shape đúng BE nhưng mất sau split arch; biến thể `05.1:249` shape khác (thiếu `tenant_id`, `pending_payment_count`, `occupied_count`, `protected_count`) → không dùng được; `StructureSafetyStore` (`:127, :194-199`) đọc/ghi các cột này. Header file ghi rõ nguồn tái tạo: `git show develop:scripts/database/microservices/03-reservation-session-service-db.sql (section 1)` + `IF NOT EXISTS` để re-apply được |
| FK | **Không có FK** (các cột `*_id` là logical ID) — không FK tới `tenants` |
| Thứ tự | **APPLY FIRST** — `reservation-structure-safety.sql` phụ thuộc bảng này (trigger thứ 4) |

### B. `reservation-structure-safety.sql` — bảng giữ an toàn sửa cấu trúc + `guard_structure_edit` (overlay 2/4)

| Trường | Nội dung |
|---|---|
| Object tạo | Bảng `structure_edit_holds` (`:23`: PK `site_id`, `token UNIQUE`); bảng `structure_site_state` (`:27`); bảng `structure_removed_resources` (`:28`, CHECK `kind IN ('slot','unit')`); function `guard_structure_edit()` (`:29-59`: `pg_advisory_xact_lock(hashtextextended(NEW.site_id::text, 0))`, chặn khi có hold `55P03`, chặn resource removed/inactive `23514`); **4 trigger `guard_structure_edit`** trên `reservations` (`:61`), `parking_sessions` (`:63`), `slot_allocations` (`:65`), `site_capacity_pools` (`:67`) — pattern `DROP TRIGGER IF EXISTS` + `CREATE TRIGGER` |
| Purpose | Gap **R1**: develop đang sống với gate-flow (`StructureSafetyStore.cs:81,92-96,127`); arch `9a13499` không có các bảng/function/trigger này → thiếu là structure-edit fail `42P01`. Reproduced từ `integration/03-reservation-structure-safety.sql` (script cũ bị runner chặn → chuyển thành overlay) |
| FK | **Không có FK vật lý** — header file ghi: *"No triggers, joins or physical FKs to Parking/User databases"*; không FK tới `tenants` |
| Thứ tự | **APPLY SECOND** — bắt buộc sau `reservation-site-capacity-pools.sql` (trigger thứ 4 tham chiếu `site_capacity_pools`); trước receipts và confirmation |

### C. `reservation-structure-receipts.sql` — biên nhận idempotency (overlay 3/4)

| Trường | Nội dung |
|---|---|
| Object tạo | Bảng `structure_release_receipts` (`:15-20`: PK `token`, `tenant_id`, `site_id`, `outcome JSONB NOT NULL`, `completed_at`) |
| Purpose | Gap **R2**: `StructureSafetyStore` tra token receipt trước khi lấy hold (`:92`, `:156`, `:214`) — arch không có bảng. Reproduced từ `05.12-Reservation-Structure-Receipts.sql` (script legacy bị chặn) |
| FK | **Không có FK** — không FK tới `tenants` |
| Thứ tự | **APPLY THIRD** — sau safety (cùng flow BE: holds/guards → receipts), trước confirmation |

### D. `reservation-confirmation-time.sql` — trigger gắn `confirmed_at` (overlay 4/4)

| Trường | Nội dung |
|---|---|
| Object tạo | Function `record_reservation_confirmation()` (`:20-29`); **trigger tên `reservation_confirmation_time`** (`:30-31`: `DROP TRIGGER IF EXISTS reservation_confirmation_time ON reservations;` + `CREATE TRIGGER reservation_confirmation_time BEFORE INSERT OR UPDATE ON reservations FOR EACH ROW EXECUTE FUNCTION record_reservation_confirmation()`) |
| Purpose | Gap **R4**: không BE nào set `confirmed_at`, chỉ đọc (`StructureSafetyStore.cs:19,23` — `SELECT/ORDER BY confirmed_at`) → thiếu trigger thì cột luôn NULL. Reproduced từ `05.10-Reservation-Confirmation-Time.sql`, **bỏ phần `ALTER TABLE ADD COLUMN confirmed_at`** vì cột đã có sẵn ở base `03b-reservation-operations-service-db.sql:62` |
| FK | Không có FK; trigger chỉ trên `reservations` (bảng cùng DB) |
| Thứ tự | **APPLY FOURTH (cuối cùng)** — header file: hai overlay trước đó cũng cài trigger `BEFORE INSERT OR UPDATE` trên `reservations`, phải cài overlay này sau để `guard_structure_edit` chạy trước |

### E. Thứ tự apply trên `reservation-db`

Compose mount vào `/docker-entrypoint-initdb.d` (tên file số = thứ tự):

| Thứ tự | Mount (compose line) | Container name |
|---|---|---|
| 1 | `microservices/03b-reservation-operations-service-db.sql` → `01-base.sql` (`:47`) | base |
| 2 | `overlay/reservation-site-capacity-pools.sql` → **`02-overlay-capacity-pools.sql`** (`:48`) | overlay 1 |
| 3 | `overlay/reservation-structure-safety.sql` → **`03-overlay-structure-safety.sql`** (`:49`) | overlay 2 |
| 4 | `overlay/reservation-structure-receipts.sql` → **`04-overlay-receipts.sql`** (`:50`) | overlay 3 |
| 5 | `overlay/reservation-confirmation-time.sql` → **`05-overlay-confirmation.sql`** (`:51`) | overlay 4 |

Mirror trong runner `scripts/apply-workflow-migrations.ps1:29-35` (`Reservation = @(03b, site-capacity-pools, structure-safety, structure-receipts, confirmation-time)`), chạy `psql -v ON_ERROR_STOP=1`; runner kiểm tra bảng bắt buộc `reservations`, `structure_edit_holds`, `site_capacity_pools`, `structure_release_receipts` (`:51`) trước khi apply.

---

## Lý do / quyết định liên quan

- **Q2** (develop dùng cả structure-sync **và** gate-flow → overlay tích hợp bắt buộc được giữ): 3/4 overlay này chính là phần gate-flow của ReservationService được "di trú" từ `integration/03` + `05.10` + `05.12` sang bộ overlay mới.
- **D7 / R3** → `reservation-site-capacity-pools.sql` (recreate shape-BE).
- **R1** → safety tables + `guard_structure_edit()` (advisory lock khớp contract tại `StructureSafetyStore.cs:81`).
- **R2** → receipts; **R4** → confirmation trigger (thiếu thì `confirmed_at` luôn NULL).
- **D1** (placement theo DB) → toàn bộ 4 file mount vào `smartpark_reservation`, không đụng `smartpark_parking`/`smartpark_user` (không cross-DB FK/join).

---

## Evidence

### E1. Danh sách object (grep file hiện hành, file:line)

```
$ Select-String -Path scripts\database\overlay\reservation-*.sql -Pattern 'CREATE TABLE|CREATE INDEX|CREATE OR REPLACE FUNCTION|CREATE TRIGGER|DROP TRIGGER'
reservation-site-capacity-pools.sql:19:   CREATE TABLE IF NOT EXISTS site_capacity_pools (
reservation-site-capacity-pools.sql:40:  CREATE INDEX IF NOT EXISTS idx_site_capacity_pools_tenant_site ON site_capacity_pools(tenant_id, site_id);
reservation-structure-safety.sql:23:     CREATE TABLE IF NOT EXISTS structure_edit_holds (
reservation-structure-safety.sql:27:     CREATE TABLE IF NOT EXISTS structure_site_state (site_id UUID PRIMARY KEY, …);
reservation-structure-safety.sql:28:     CREATE TABLE IF NOT EXISTS structure_removed_resources (id UUID PRIMARY KEY, …);
reservation-structure-safety.sql:29:     CREATE OR REPLACE FUNCTION guard_structure_edit() RETURNS trigger LANGUAGE plpgsql AS $$
reservation-structure-safety.sql:61:     CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON reservations …
reservation-structure-safety.sql:63:     CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON parking_sessions …
reservation-structure-safety.sql:65:     CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON slot_allocations …
reservation-structure-safety.sql:67:     CREATE TRIGGER guard_structure_edit BEFORE INSERT OR UPDATE ON site_capacity_pools …
reservation-structure-receipts.sql:15:   CREATE TABLE IF NOT EXISTS structure_release_receipts (
reservation-confirmation-time.sql:20:    CREATE OR REPLACE FUNCTION record_reservation_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
reservation-confirmation-time.sql:30:    DROP TRIGGER IF EXISTS reservation_confirmation_time ON reservations;
reservation-confirmation-time.sql:31:    CREATE TRIGGER reservation_confirmation_time BEFORE INSERT OR UPDATE ON reservations
                                         FOR EACH ROW EXECUTE FUNCTION record_reservation_confirmation();
```

→ Tên trigger xác nhận: **`reservation_confirmation_time`** trên `reservations`, function **`record_reservation_confirmation()`**.

### E2. Không có FK (đặc biệt không FK tới `tenants`)

```
$ Select-String -Path scripts\database\overlay\reservation-*.sql -Pattern 'REFERENCES'
reservation-site-capacity-pools.sql:21:   -- Cross-domain References (Logical IDs)      # chỉ là comment
reservation-structure-safety.sql:9:       -- … because the 4th trigger references site_capacity_pools …   # chỉ là comment
(0 mệnh đề REFERENCES/FOREIGN KEY trong SQL)
```

### E3. Thứ tự mount compose (nguyên văn, cắt …)

```
$ git diff -- compose.schema-integration.yml        # (trích khối reservation-db)
@@ -44,10 +44,11 @@ services:
     volumes:
       - reservation-schema-data:/var/lib/postgresql/data
-      - ./scripts/database/microservices/03-reservation-session-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
-      - ./scripts/database/integration/03-reservation-structure-safety.sql:/docker-entrypoint-initdb.d/02-alignment.sql:ro
-      - ./scripts/database/05.10-Reservation-Confirmation-Time.sql:/docker-entrypoint-initdb.d/03-workflows.sql:ro
-      - ./scripts/database/05.12-Reservation-Structure-Receipts.sql:/docker-entrypoint-initdb.d/04-receipts.sql:ro
+      - ./scripts/database/microservices/03b-reservation-operations-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
+      - ./scripts/database/overlay/reservation-site-capacity-pools.sql:/docker-entrypoint-initdb.d/02-overlay-capacity-pools.sql:ro
+      - ./scripts/database/overlay/reservation-structure-safety.sql:/docker-entrypoint-initdb.d/03-overlay-structure-safety.sql:ro
+      - ./scripts/database/overlay/reservation-structure-receipts.sql:/docker-entrypoint-initdb.d/04-overlay-receipts.sql:ro
+      - ./scripts/database/overlay/reservation-confirmation-time.sql:/docker-entrypoint-initdb.d/05-overlay-confirmation.sql:ro
```

```
$ scripts/apply-workflow-migrations.ps1:29-35
    Reservation = @(
        'microservices/03b-reservation-operations-service-db.sql',
        'overlay/reservation-site-capacity-pools.sql',
        'overlay/reservation-structure-safety.sql',
        'overlay/reservation-structure-receipts.sql',
        'overlay/reservation-confirmation-time.sql'
    )
```

### E4. Bằng chứng purpose / thứ tự (comment header của file — file:line)

```
reservation-site-capacity-pools.sql:3-5:
-- Purpose : Recreate site_capacity_pools with the shape the ReservationService BE
--           actually uses. The original DDL … was lost in the arch SRS split (T4 D7, gap R3) …
reservation-site-capacity-pools.sql:8:  -- Order   : APPLY FIRST (1st) — reservation-structure-safety.sql depends on this table.

reservation-structure-safety.sql:3-5:
-- Purpose : Structure-edit hold/state/removed-resource tables + the guard_structure_edit()
--           trigger function and its 4 triggers (reservations, parking_sessions,
--           slot_allocations, site_capacity_pools). Missing … (plan gap R1).
reservation-structure-safety.sql:10-11: -- Order   : APPLY SECOND (2nd) — MUST run AFTER reservation-site-capacity-pools.sql
                                        --           because the 4th trigger references site_capacity_pools. …

reservation-structure-receipts.sql:3-5: -- Purpose : structure_release_receipts … (plan gap R2). … APPLY THIRD (3rd) …
reservation-confirmation-time.sql:3-6:  -- Purpose : record_reservation_confirmation() + reservation_confirmation_time trigger …
                                        --           (plan gap R4) … APPLY FOURTH (4th) …
reservation-confirmation-time.sql:13-16:-- … the ALTER TABLE ... ADD COLUMN confirmed_at is intentionally OMITTED — the column
                                        -- already exists on reservations in the new base file
                                        -- 03b-reservation-operations-service-db.sql, so no ALTER is needed (R4).
```

```
# cột confirmed_at có sẵn ở base 03b:
scripts/database/microservices/03b-reservation-operations-service-db.sql:62:     confirmed_at TIMESTAMPTZ,
```

---

## Ảnh hưởng

- **Vòng đời reservation phục hồi đầy đủ:** `confirmed_at` tự gắn khi vào trạng thái `CONFIRMED/ALLOCATED/PARKING` (T6 ⑫); `guard_structure_edit` chặn ghi đè dữ liệu khi đang sửa cấu trúc (T6 ⑦⑬); receipts giữ idempotency cho commit flow.
- **Không cross-DB:** 4 overlay chỉ thao tác trong `smartpark_reservation`, không FK/join sang parking/user → đúng D1 (placement) và không cần sửa BE data-access.
- **Thứ tự là bắt buộc:** đảo thứ tự (safety trước capacity-pools) → lỗi `42703`/`42P01` khi tạo trigger thứ 4; confirmation trước safety → thứ tự trigger trên `reservations` không như thiết kế (file ghi rõ lý do).
- **Idempotency:** `IF NOT EXISTS` / `CREATE OR REPLACE` / `DROP TRIGGER IF EXISTS` → chạy lại được; mỗi file bọc `BEGIN/COMMIT` + runner `ON_ERROR_STOP=1` → fail là rollback toàn file.
- **Đối chiếu regression:** `05.1`, `05.10`, `05.12`, `integration/03` cũ **không được chạy song song** (runner chặn `05.[1-9]` + `integration/0[123]` tại `apply-workflow-migrations.ps1:59`) — nếu mount tay sẽ double-apply/conflict.

---

## Cách tái kiểm chứng

```powershell
# 1) File + thứ tự mount (chỉ đọc)
Get-ChildItem scripts\database\overlay -Filter 'reservation-*.sql' | Select-Object Name, Length
Select-String -Path compose.schema-integration.yml -Pattern 'overlay/reservation'

# 2) Object + trigger name + không có FK
Select-String -Path scripts\database\overlay\reservation-*.sql -Pattern 'CREATE TABLE|CREATE TRIGGER|CREATE OR REPLACE FUNCTION'
Select-String -Path scripts\database\overlay\reservation-*.sql -Pattern 'REFERENCES\s'   # -> chỉ ra comment, 0 FK thật
Select-String -Path scripts\database\microservices\03b-reservation-operations-service-db.sql -Pattern 'confirmed_at'  # -> :62

# 3) Runtime (sau setup.ps1/compose up) — psql chỉ đọc
docker compose --project-name smartpark-schema -f compose.schema-integration.yml exec -T reservation-db `
  psql -U smartpark_reservation -d smartpark_reservation -At -c `
  "SELECT table_name FROM information_schema.tables WHERE table_name IN ('site_capacity_pools','structure_edit_holds','structure_site_state','structure_removed_resources','structure_release_receipts') ORDER BY 1;"
# -> 5 dòng

docker compose --project-name smartpark-schema -f compose.schema-integration.yml exec -T reservation-db `
  psql -U smartpark_reservation -d smartpark_reservation -At -c `
  "SELECT tgname FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('guard_structure_edit','reservation_confirmation_time') ORDER BY 1;"
# -> guard_structure_edit (×4 bảng) và reservation_confirmation_time

# 4) Trigger hoạt động (đọc dữ liệu): insert thử theo checklist T6 ⑫ rồi kiểm tra
#    SELECT confirmed_at FROM reservations WHERE id = '<id>';   # không NULL khi status CONFIRMED/ALLOCATED/PARKING
```
